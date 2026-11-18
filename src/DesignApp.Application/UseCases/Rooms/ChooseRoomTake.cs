using DesignApp.Application.Helpers;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;
using Microsoft.Extensions.Logging;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.UseCases;

/// <summary>
/// Use case: one of a room's photos becomes the room's photo, the one the editor analyzes. A room already analyzed with
/// another photo waits for its analysis again: its outlines, its removals and the colors its designs gave it belonged to
/// the other photo, so they are dropped (the files too). Choosing the room's own photo again after its analysis failed
/// lets it be analyzed afresh.
/// </summary>
/// <param name="rooms">The room database.</param>
/// <param name="designs">The designs that may hold the room's colors.</param>
/// <param name="currentUser">Whose rooms may be changed.</param>
/// <param name="takes">Copies the photo.</param>
/// <param name="storage">Where the dropped files are.</param>
/// <param name="clock">When the designs change.</param>
/// <param name="logger">Records files that couldn't be deleted.</param>
public sealed partial class ChooseRoomTake(
    IRoomRepository rooms, IDesignRepository designs, ICurrentUser currentUser, TakeStore takes, IFileStorage storage, TimeProvider clock,
    ILogger<ChooseRoomTake> logger)
{
    /// <summary>Makes the photo the room's.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="takeId">One of its photos.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The room, a failure the user can read, or <c>null</c> when the user has no such room or photo.</returns>
    public async Task<Result<Room>?> HandleAsync(Guid roomId, Guid takeId, CancellationToken cancellationToken)
    {
        var room = await rooms.FindAsync(roomId, currentUser.Id, cancellationToken);
        var take = room is null ? null : await rooms.FindTakeAsync(roomId, takeId, cancellationToken);

        if (take is null)
        {
            return null;
        }

        if (room!.ChosenTakeId == take.Id && room.Status is not RoomStatus.Failed)
        {
            return room;
        }

        if (room.Status is RoomStatus.Uploaded or RoomStatus.Analyzing or RoomStatus.Segmenting)
        {
            return Result.Failure<Room>(Domain.Constants.Messages.CannotChooseTake(room.Status));
        }

        var dropped = OldFiles(room).ToList();

        await takes.UseAsRoomPhotoAsync(take, cancellationToken);
        room.ChooseTake(take);
        var holding = await designs.ListHoldingRoomAsync(room.Id, room.OwnerId, cancellationToken);

        foreach (var design in holding)
        {
            design.SetRoomEdits(room, [], [], clock.GetUtcNow());
        }

        await rooms.SaveChangesAsync(cancellationToken);
        await designs.SaveChangesAsync(cancellationToken);

        foreach (var key in dropped)
        {
            try
            {
                await storage.DeleteAsync(key, cancellationToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                LogFileNotDeleted(error, key);
            }
        }

        return room;
    }

    /// <summary>The files of an analyzed room's outlines and removals, which the new photo doesn't use.</summary>
    /// <param name="room">The room.</param>
    private static IEnumerable<string> OldFiles(Room room)
    {
        if (room.Status is not RoomStatus.Ready)
        {
            yield break;
        }

        for (var revision = 0; revision <= room.LatestLabelMapRevision; revision++)
        {
            yield return Constants.Rooms.LabelMapKey(room.Id, revision);
        }

        for (var version = 1; version <= room.LatestPhotoVersion; version++)
        {
            yield return Constants.Rooms.PhotoKey(room.Id, version);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't delete the file {Key} of a room's earlier photo")]
    private partial void LogFileNotDeleted(Exception error, string key);
}
