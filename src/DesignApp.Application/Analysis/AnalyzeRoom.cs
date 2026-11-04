using DesignApp.Application.Abstractions;
using DesignApp.Application.Rooms;
using DesignApp.Domain.Rooms;
using Microsoft.Extensions.Logging;

namespace DesignApp.Application.Analysis;

/// <summary>
/// Use case: run the AI pipeline on an uploaded room. Claude lists the objects, SAM 3 outlines them,
/// and the outlines become a label map. Every status change is saved and sent to the browser.
/// </summary>
/// <param name="rooms">The room database.</param>
/// <param name="storage">Where the photo and the label map are stored.</param>
/// <param name="imaging">Scales the photo and builds the label map.</param>
/// <param name="inventory">Step 1: lists the objects.</param>
/// <param name="segmentation">Step 2: outlines them.</param>
/// <param name="notifier">Pushes each change to the browser.</param>
/// <param name="logger">Records why an analysis failed.</param>
public sealed partial class AnalyzeRoom(
    IRoomRepository rooms,
    IFileStorage storage,
    IRoomImaging imaging,
    IObjectInventoryProvider inventory,
    ISegmentationProvider segmentation,
    IRoomNotifier notifier,
    ILogger<AnalyzeRoom> logger)
{
    /// <summary>Shown when something went wrong on our side; the details go to the log only.</summary>
    public const string UnexpectedFailure = "We couldn't analyze this photo. Try uploading it again in a moment.";

    /// <summary>Shown when no object survived segmentation.</summary>
    public const string NothingFound = "We couldn't find any objects to restyle in this photo. Try a wider shot of the room.";

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
        if (room.AnalysisAttempts >= Room.MaxAnalysisAttempts)
        {
            await FailAsync(room, UnexpectedFailure, cancellationToken);
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
                await FailAsync(room, UnexpectedFailure, CancellationToken.None);
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

        var listed = await inventory.ListObjectsAsync(image, cancellationToken);
        if (!listed.Succeeded)
        {
            await FailAsync(room, listed.Error, cancellationToken);
            return;
        }
        var items = listed.Value.Take(AnalysisRules.MaxObjects).ToList();
        if (items.Count == 0)
        {
            await FailAsync(room, NothingFound, cancellationToken);
            return;
        }

        room.StartSegmentation();
        await SaveAndNotifyAsync(room, cancellationToken);

        var masks = await segmentation.SegmentAsync(image, items, cancellationToken);
        var outlined = items.Zip(masks)
            .Where(pair => pair.Second is not null)
            .Select(pair => new SegmentedObject(pair.First, pair.Second!))
            .ToList();
        var labelMap = imaging.BuildLabelMap(image, outlined);
        if (labelMap.Objects.Count == 0)
        {
            await FailAsync(room, NothingFound, cancellationToken);
            return;
        }

        var key = StorageKeys.RoomLabelMap(room.Id);
        using (var content = new MemoryStream(labelMap.Png, writable: false))
            await storage.SaveAsync(key, content, "image/png", cancellationToken);

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
