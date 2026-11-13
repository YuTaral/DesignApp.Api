using DesignApp.Application.Helpers;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;
using Microsoft.Extensions.Logging;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.UseCases;

/// <summary>
/// Use case: give a room its depth map for the 3D view. It is measured once, from the original photo, with the room's
/// AI (with the real AI, one paid call), stored, and reused from then on. Sample rooms bring theirs from the library.
/// </summary>
/// <param name="rooms">The room database.</param>
/// <param name="storage">Where the photo, label map and depth map are.</param>
/// <param name="ai">The room's AI, for its depth provider.</param>
/// <param name="currentUser">Whose room it must be.</param>
/// <param name="locks">Lets one request at a time measure a room, so a second one waits and reuses the result.</param>
/// <param name="logger">Records a depth map that couldn't be cleaned up.</param>
public sealed partial class MeasureRoomDepth(
    IRoomRepository rooms,
    IFileStorage storage,
    IAiProviders ai,
    ICurrentUser currentUser,
    RoomLocks locks,
    ILogger<MeasureRoomDepth> logger)
{
    /// <summary>Returns the room with its depth map, measuring it first if it has none.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The room, a failure the user can read, or <c>null</c> when the user has no such room.</returns>
    public async Task<Result<Room>?> HandleAsync(Guid roomId, CancellationToken cancellationToken)
    {
        // A second request for the same room (a double click, two tabs) waits here, then finds the depth map stored.
        using var held = await locks.LockAsync(roomId, cancellationToken);
        var room = await rooms.FindAsync(roomId, currentUser.Id, cancellationToken);
        if (room is null)
            return null;
        if (room.Status is not RoomStatus.Ready)
            return Result.Failure<Room>(Constants.Messages.DepthNotReady);
        if (room.DepthKey is not null)
            return room;

        var photo = await ReadAsync(Constants.Rooms.PhotoKey(room.Id), cancellationToken);
        var labelMap = await ReadAsync(Constants.Rooms.LabelMapKey(room.Id), cancellationToken);
        if (photo is null || labelMap is null)
            throw new InvalidOperationException($"The photo or label map of room {room.Id} is missing from storage.");

        var measured = await ai.Depth(room.AiMode).MeasureAsync(
            new DepthInput(photo, room.Width, room.Height, labelMap, room.Objects), cancellationToken);
        if (!measured.Succeeded)
            return Result.Failure<Room>(measured.Error);

        var key = Constants.Rooms.DepthKey(room.Id);
        using (var content = new MemoryStream(measured.Value.Png, writable: false))
            await storage.SaveAsync(key, content, Constants.ContentTypes.Png, cancellationToken);
        try
        {
            room.SetDepth(key, measured.Value.FovX, measured.Value.FovY);
            await rooms.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await DeleteQuietlyAsync(key);
            throw;
        }
        return room;
    }

    /// <summary>Reads a stored file into memory.</summary>
    /// <param name="key">The storage key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes, or <c>null</c> when the file is missing.</returns>
    private async Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        await using var file = await storage.OpenReadAsync(key, cancellationToken);
        if (file is null)
            return null;
        using var buffer = new MemoryStream();
        await file.Content.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    /// <summary>Deletes a depth map that couldn't be recorded. A failed delete is only logged.</summary>
    /// <param name="key">The storage key.</param>
    private async Task DeleteQuietlyAsync(string key)
    {
        try
        {
            await storage.DeleteAsync(key, CancellationToken.None);
        }
        catch (Exception error)
        {
            LogNotDeleted(error, key);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't delete the unrecorded depth map {Key}")]
    private partial void LogNotDeleted(Exception error, string key);
}
