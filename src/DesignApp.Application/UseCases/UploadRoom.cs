using DesignApp.Application.Abstractions;
using DesignApp.Application.Analysis;
using DesignApp.Application.Common;
using DesignApp.Application.Photos;
using DesignApp.Domain.Rooms;
using static DesignApp.Domain.Constants;

namespace DesignApp.Application.Rooms;

/// <summary>Use case: turn an uploaded photo into a new room.</summary>
/// <param name="photoProcessor">Validates and normalizes the photo.</param>
/// <param name="storage">Where the photo file is stored.</param>
/// <param name="rooms">The room database.</param>
/// <param name="currentUser">Who will own the room.</param>
/// <param name="clock">Supplies the upload time (replaceable in tests).</param>
/// <param name="analysisScheduler">Starts the AI analysis once the room is saved.</param>
public sealed class UploadRoom(
    IPhotoProcessor photoProcessor,
    IFileStorage storage,
    IRoomRepository rooms,
    ICurrentUser currentUser,
    TimeProvider clock,
    IAnalysisScheduler analysisScheduler)
{
    /// <summary>Processes the photo, stores it, saves the room and queues its analysis. Removes the photo again if the save fails.</summary>
    /// <param name="upload">The uploaded file's bytes.</param>
    /// <param name="cancellationToken">Cancels the upload.</param>
    /// <param name="scope">Which objects the analysis should look for.</param>
    /// <param name="aiMode">Which AI analyzes the photo (the caller checks that it's allowed).</param>
    /// <returns>The new room, or a failure saying why the photo can't be used.</returns>
    public async Task<Result<Room>> HandleAsync(Stream upload, CancellationToken cancellationToken,
        AnalysisScope scope = AnalysisScope.WholeRoom, AiMode aiMode = AiMode.Fake)
    {
        var processed = photoProcessor.Process(upload);
        if (!processed.Succeeded)
            return Result.Failure<Room>(processed.Error);
        var photo = processed.Value;

        var now = clock.GetUtcNow();
        var id = Guid.CreateVersion7(now);
        var photoKey = Constants.Rooms.PhotoKey(id);
        var room = Room.Upload(id, currentUser.Id, photoKey, photo.Width, photo.Height, now, scope, aiMode);

        using (var content = new MemoryStream(photo.Content, writable: false))
            await storage.SaveAsync(photoKey, content, Constants.ContentTypes.Jpeg, cancellationToken);

        rooms.Add(room);
        try
        {
            await rooms.SaveChangesAsync(cancellationToken);
        }
        catch (Exception saveError)
        {
            // Don't leave an orphaned photo behind when the room can't be saved,
            // but report the save failure even if the cleanup fails too.
            try
            {
                await storage.DeleteAsync(photoKey, CancellationToken.None);
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException(saveError, cleanupError);
            }
            throw;
        }

        analysisScheduler.Schedule(room.Id);
        return room;
    }
}
