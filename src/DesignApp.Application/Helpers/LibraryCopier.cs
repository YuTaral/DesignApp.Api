using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;
using Microsoft.Extensions.Logging;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.Helpers;

/// <summary>
/// Copies built-in library rooms into rooms of the current user: the photo and label map are stored under the new
/// room's keys and the room starts Ready, so no AI runs. The caller saves the rooms; if that fails, it calls
/// <see cref="DeleteFilesAsync"/> so no files are left behind.
/// </summary>
/// <param name="library">The library's files.</param>
/// <param name="storage">Where the copies are stored.</param>
/// <param name="rooms">The room database.</param>
/// <param name="currentUser">Who will own the copies.</param>
/// <param name="clock">Supplies the copy time.</param>
/// <param name="logger">Records files that couldn't be cleaned up.</param>
public sealed partial class LibraryCopier(
    ILibrary library,
    IFileStorage storage,
    IRoomRepository rooms,
    ICurrentUser currentUser,
    TimeProvider clock,
    ILogger<LibraryCopier> logger)
{
    private readonly List<string> _stored = [];

    /// <summary>Stores a copy of one library layout's files and adds the new room (not saved yet).</summary>
    /// <param name="design">The library design.</param>
    /// <param name="source">One of its rooms.</param>
    /// <param name="layout">One of the room's layouts.</param>
    /// <param name="aiMode">The AI the copy's later steps use (removing an object).</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>The new room, Ready.</returns>
    public async Task<Room> CopyAsync(LibraryDesign design, LibraryRoom source, LibraryLayout layout, AiMode aiMode, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var id = Guid.CreateVersion7(now);
        var photoKey = Constants.Rooms.PhotoKey(id);
        var labelMapKey = Constants.Rooms.LabelMapKey(id);

        await using (var photo = library.OpenPhoto(design.Id, source.Id, layout.Id))
            await storage.SaveAsync(photoKey, photo, Constants.ContentTypes.Jpeg, cancellationToken);
        _stored.Add(photoKey);
        await using (var labelMap = library.OpenLabelMap(design.Id, source.Id, layout.Id))
            await storage.SaveAsync(labelMapKey, labelMap, Constants.ContentTypes.Png, cancellationToken);
        _stored.Add(labelMapKey);

        var room = Room.FromLibrary(id, currentUser.Id, photoKey, layout.Width, layout.Height, now, layout.Objects,
            labelMapKey, Constants.Library.RoomName(design.Id, source.Id, layout.Id), aiMode);
        rooms.Add(room);
        return room;
    }

    /// <summary>Deletes every file this copier stored, after the rooms couldn't be saved. A failed delete is only logged.</summary>
    public async Task DeleteFilesAsync()
    {
        foreach (var key in _stored)
        {
            try
            {
                await storage.DeleteAsync(key, CancellationToken.None);
            }
            catch (Exception error)
            {
                LogFileNotDeleted(error, key);
            }
        }
        _stored.Clear();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't delete the copied library file {Key}")]
    private partial void LogFileNotDeleted(Exception error, string key);
}
