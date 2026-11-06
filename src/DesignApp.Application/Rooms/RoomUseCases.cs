using DesignApp.Application.Abstractions;
using DesignApp.Application.Analysis;
using DesignApp.Application.Common;
using DesignApp.Application.Photos;
using DesignApp.Domain.Rooms;
using Microsoft.Extensions.Logging;
using static DesignApp.Application.Constants;
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

/// <summary>Use case: load one of the current user's rooms.</summary>
/// <param name="rooms">The room database.</param>
/// <param name="currentUser">Whose rooms may be returned.</param>
public sealed class GetRoom(IRoomRepository rooms, ICurrentUser currentUser)
{
    /// <summary>Loads the room.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The current user's room, or <c>null</c> if they have no room with that id.</returns>
    public Task<Room?> HandleAsync(Guid id, CancellationToken cancellationToken) =>
        rooms.FindAsync(id, currentUser.Id, cancellationToken);
}

/// <summary>
/// Use case: run the AI pipeline on an uploaded room. Claude lists the objects, SAM 3 outlines them,
/// and the outlines become a label map. Every status change is saved and sent to the browser.
/// </summary>
/// <param name="rooms">The room database.</param>
/// <param name="storage">Where the photo and the label map are stored.</param>
/// <param name="imaging">Scales the photo and builds the label map.</param>
/// <param name="ai">The providers of the AI the room was uploaded with: step 1 lists the objects, step 2 outlines them.</param>
/// <param name="notifier">Pushes each change to the browser.</param>
/// <param name="logger">Records why an analysis failed.</param>
public sealed partial class AnalyzeRoom(
    IRoomRepository rooms,
    IFileStorage storage,
    IRoomImaging imaging,
    IAiProviders ai,
    IRoomNotifier notifier,
    ILogger<AnalyzeRoom> logger)
{
    /// <summary>Analyzes the room, unless it is already finished. Safe to call twice for the same room.</summary>
    /// <param name="roomId">The room to analyze.</param>
    /// <param name="cancellationToken">
    /// Cancelled when the API shuts down. The room is then left as it is and analyzed again on the next start.
    /// </param>
    public async Task HandleAsync(Guid roomId, CancellationToken cancellationToken)
    {
        var room = await rooms.GetForAnalysisAsync(roomId, cancellationToken);
        if (room is null || room.Status is RoomStatus.Ready or RoomStatus.Failed)
            return;

        if (room.Status is not RoomStatus.Uploaded)
            room.RestartAnalysis(); // a previous run was cut off
        if (room.AnalysisAttempts >= Domain.Constants.Rooms.MaxAnalysisAttempts)
        {
            await FailAsync(room, Constants.Messages.UnexpectedFailure, cancellationToken);
            return;
        }

        room.StartAnalysis();
        await SaveAndNotifyAsync(room, cancellationToken);

        try
        {
            await RunPipelineAsync(room, cancellationToken);
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested)
        {
            LogAnalysisFailed(error, room.Id);
            // If the failure came after the room was finished (a save that failed), there is nothing left to mark.
            if (room.Status is not (RoomStatus.Ready or RoomStatus.Failed))
                await FailAsync(room, Constants.Messages.UnexpectedFailure, CancellationToken.None);
        }
    }

    /// <summary>The steps after Analyzing: inventory, segmentation, label map.</summary>
    /// <param name="room">The room, already marked Analyzing.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    private async Task RunPipelineAsync(Room room, CancellationToken cancellationToken)
    {
        AnalysisImage image;
        await using (var photo = await storage.OpenReadAsync(room.PhotoKey, cancellationToken)
            ?? throw new InvalidOperationException($"The photo of room {room.Id} is missing from storage."))
        {
            image = imaging.PrepareForAnalysis(photo.Content);
        }

        var listed = await ai.Inventory(room.AiMode).ListObjectsAsync(image, cancellationToken);
        if (!listed.Succeeded)
        {
            await FailAsync(room, listed.Error, cancellationToken);
            return;
        }
        // Objects outside the scope the user chose are never outlined, which also saves their SAM 3 requests.
        var items = listed.Value.Where(item => room.Scope.Includes(item.Category)).Take(Constants.Analysis.MaxObjects).ToList();
        if (items.Count == 0)
        {
            await FailAsync(room, Constants.Messages.NothingFound, cancellationToken);
            return;
        }

        room.StartSegmentation();
        await SaveAndNotifyAsync(room, cancellationToken);

        var masks = await ai.Segmentation(room.AiMode).SegmentAsync(image, items, cancellationToken);
        var outlined = items.Zip(masks)
            .Where(pair => pair.Second is not null)
            .Select(pair => new SegmentedObject(pair.First, pair.Second!))
            .ToList();
        var labelMap = imaging.BuildLabelMap(image, outlined);
        if (labelMap.Objects.Count == 0)
        {
            await FailAsync(room, Constants.Messages.NothingFound, cancellationToken);
            return;
        }

        var key = Constants.Rooms.LabelMapKey(room.Id);
        using (var content = new MemoryStream(labelMap.Png, writable: false))
            await storage.SaveAsync(key, content, Constants.ContentTypes.Png, cancellationToken);

        room.MarkReady(labelMap.Objects, key);
        await SaveAndNotifyAsync(room, cancellationToken);
    }

    /// <summary>Marks the room failed with a reason the user sees, then saves and notifies.</summary>
    /// <param name="room">The room.</param>
    /// <param name="reason">Shown to the user.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    private async Task FailAsync(Room room, string reason, CancellationToken cancellationToken)
    {
        room.MarkFailed(reason);
        await SaveAndNotifyAsync(room, cancellationToken);
    }

    /// <summary>Saves the room, then tells the browser. A failed notification only loses a live update.</summary>
    /// <param name="room">The changed room.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    private async Task SaveAndNotifyAsync(Room room, CancellationToken cancellationToken)
    {
        await rooms.SaveChangesAsync(cancellationToken);
        try
        {
            await notifier.RoomChangedAsync(room, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            LogNotifyFailed(error, room.Id);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Analysis of room {RoomId} failed")]
    private partial void LogAnalysisFailed(Exception error, Guid roomId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't send the live update for room {RoomId}")]
    private partial void LogNotifyFailed(Exception error, Guid roomId);
}

/// <summary>
/// Use case: the user fixes an object's outline by painting or erasing it with a brush. Each stroke
/// is stored as a new label map version, so the editor's undo can switch back to an earlier one.
/// </summary>
/// <param name="rooms">The room database.</param>
/// <param name="currentUser">Whose rooms may be changed.</param>
/// <param name="storage">Where the label maps are stored.</param>
/// <param name="imaging">Does the pixel work.</param>
public sealed class FixRoomOutline(IRoomRepository rooms, ICurrentUser currentUser, IFileStorage storage, IRoomImaging imaging)
{
    /// <summary>Paints or erases an object's outline with a brush stroke, starting from a given label map version.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="baseRevision">The label map version the user drew on.</param>
    /// <param name="objectIndex">The object to change.</param>
    /// <param name="mode">Paint or erase.</param>
    /// <param name="radius">The brush radius, in label map pixels.</param>
    /// <param name="points">The stroke's path, 0..1 of the photo.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The room with its new current version, a failure the user can read, or <c>null</c> when the user has no such room.</returns>
    public async Task<Result<Room>?> FixAsync(
        Guid roomId, int baseRevision, int objectIndex, OutlineBrushMode mode, double radius, IReadOnlyList<StrokePoint> points,
        CancellationToken cancellationToken)
    {
        var room = await rooms.FindAsync(roomId, currentUser.Id, cancellationToken);
        if (room is null)
            return null;
        if (room.Status is not RoomStatus.Ready)
            return Result.Failure<Room>(Constants.Messages.OutlinesNotReady);
        if (!room.HasLabelMapRevision(baseRevision))
            return Result.Failure<Room>(Constants.Messages.UnknownRevision);
        var target = room.Objects.FirstOrDefault(o => o.Index == objectIndex);
        if (target is null)
            return Result.Failure<Room>(Constants.Messages.UnknownStrokeObject);
        if (points.Count is 0 or > Constants.Strokes.MaxPoints)
            return Result.Failure<Room>(Constants.Messages.StrokePoints);
        if (double.IsNaN(radius) || radius is < Constants.Strokes.MinRadius or > Constants.Strokes.MaxRadius)
            return Result.Failure<Room>(Constants.Messages.BrushRadius);
        if (points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y) || p.X is < Constants.Strokes.MinPoint or > Constants.Strokes.MaxPoint || p.Y is < Constants.Strokes.MinPoint or > Constants.Strokes.MaxPoint))
            return Result.Failure<Room>(Constants.Messages.StrokeOutside);

        byte[] labelMap;
        await using (var stored = await storage.OpenReadAsync(Constants.Rooms.LabelMapKey(room.Id, baseRevision), cancellationToken)
            ?? throw new InvalidOperationException($"Label map {baseRevision} of room {room.Id} is missing from storage."))
        {
            using var buffer = new MemoryStream();
            await stored.Content.CopyToAsync(buffer, cancellationToken);
            labelMap = buffer.ToArray();
        }

        var painted = imaging.PaintOutline(labelMap, new OutlineStroke(target.Index, target.Label, mode, radius, points));
        if (!painted.Succeeded)
            return Result.Failure<Room>(painted.Error);

        // The new version is stored before the room points at it, so a failed save leaves only an unused file.
        var revision = room.LatestLabelMapRevision + 1;
        using (var content = new MemoryStream(painted.Value, writable: false))
            await storage.SaveAsync(Constants.Rooms.LabelMapKey(room.Id, revision), content, Constants.ContentTypes.Png, cancellationToken);
        room.AddLabelMapRevision(r => Constants.Rooms.LabelMapKey(room.Id, r));
        await rooms.SaveChangesAsync(cancellationToken);
        return room;
    }

    /// <summary>Makes a stored label map version current again (undo or redo of a fix).</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="revision">The version to show.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The room, a failure the user can read, or <c>null</c> when the user has no such room.</returns>
    public async Task<Result<Room>?> UseRevisionAsync(Guid roomId, int revision, CancellationToken cancellationToken)
    {
        var room = await rooms.FindAsync(roomId, currentUser.Id, cancellationToken);
        if (room is null)
            return null;
        if (!room.HasLabelMapRevision(revision))
            return Result.Failure<Room>(Constants.Messages.UnknownRevision);
        if (room.LabelMapRevision != revision)
        {
            room.UseLabelMapRevision(revision, r => Constants.Rooms.LabelMapKey(room.Id, r));
            await rooms.SaveChangesAsync(cancellationToken);
        }
        return room;
    }
}

/// <summary>
/// Deletes uploads that nobody saved: rooms older than <see cref="Constants.Rooms.UnsavedLifetime"/> that aren't in any
/// design. Only rooms in a design are kept for good.
/// </summary>
/// <param name="rooms">Finds the unsaved rooms.</param>
/// <param name="remover">Deletes them with their files.</param>
/// <param name="clock">Says what "older than" means now.</param>
public sealed class DeleteUnsavedRooms(IRoomRepository rooms, RoomRemover remover, TimeProvider clock)
{
    /// <summary>Deletes one batch of expired unsaved rooms.</summary>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many rooms were deleted.</returns>
    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var expired = await rooms.ListUnsavedBeforeAsync(clock.GetUtcNow() - Constants.Rooms.UnsavedLifetime, Constants.Rooms.CleanupBatchSize, cancellationToken);
        if (expired.Count > 0)
            await remover.DeleteAsync(expired, cancellationToken);
        return expired.Count;
    }
}

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
            yield return Constants.Rooms.LabelMapKey(room.Id, revision);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't delete the file {Key} of a deleted room")]
    private partial void LogFileNotDeleted(Exception error, string key);
}
