using DesignApp.Application.Abstractions;
using DesignApp.Domain.Rooms;
using Microsoft.Extensions.Logging;

namespace DesignApp.Application.Rooms;

/// <summary>
/// Deletes rooms completely: the database rows first, then their files (the photo and every
/// version of the label map). A file that can't be deleted is logged and left behind rather than
/// failing the request; the room is gone either way.
/// </summary>
/// <param name="rooms">Deletes the rows.</param>
/// <param name="storage">Deletes the files.</param>
/// <param name="logger">Records files that couldn't be deleted.</param>
public sealed partial class RoomRemover(IRoomRepository rooms, IFileStorage storage, ILogger<RoomRemover> logger)
{
    /// <summary>Deletes the rooms. Other tracked changes (such as the design they leave) are saved with them.</summary>
    /// <param name="doomed">The rooms to delete.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public async Task DeleteAsync(IReadOnlyList<Room> doomed, CancellationToken cancellationToken)
    {
        foreach (var room in doomed)
            rooms.Remove(room);
        await rooms.SaveChangesAsync(cancellationToken);

        foreach (var room in doomed)
        {
            foreach (var key in FilesOf(room))
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
        }
    }

    /// <summary>Every file a room may have.</summary>
    /// <param name="room">The room.</param>
    private static IEnumerable<string> FilesOf(Room room)
    {
        yield return room.PhotoKey;
        for (var revision = 0; revision <= room.LatestLabelMapRevision; revision++)
            yield return StorageKeys.RoomLabelMap(room.Id, revision);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't delete the file {Key} of a deleted room")]
    private partial void LogFileNotDeleted(Exception error, string key);
}
