using DesignApp.Application.Helpers;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Extensions;
using Microsoft.Extensions.Logging;
using static DesignApp.Domain.Constants;

namespace DesignApp.Application.UseCases;

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
        {
            return;
        }

        if (room.Status is not RoomStatus.Uploaded)
        {
            room.RestartAnalysis(); // a previous run was cut off
        }

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
            {
                await FailAsync(room, Constants.Messages.UnexpectedFailure, CancellationToken.None);
            }
        }
    }

    /// <summary>The steps after Analyzing: inventory, segmentation, label map.</summary>
    /// <param name="room">The room, already marked Analyzing.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    private async Task RunPipelineAsync(Room room, CancellationToken cancellationToken)
    {
        var image = await AnalysisPhoto.ReadAsync(storage, imaging, room, cancellationToken);
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

        var outlined = await ai.Segmentation(room.AiMode).OutlineAsync(image, items, cancellationToken);
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
