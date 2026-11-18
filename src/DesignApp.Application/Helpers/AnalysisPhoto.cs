using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;

namespace DesignApp.Application.Helpers;

/// <summary>A room's uploaded photo, read from storage and prepared for the AI (the analysis, or reading it into a plan).</summary>
public static class AnalysisPhoto
{
    /// <summary>Reads and prepares the room's photo.</summary>
    /// <param name="storage">Where photos are kept.</param>
    /// <param name="imaging">Prepares the photo.</param>
    /// <param name="room">The room.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The photo, prepared.</returns>
    /// <exception cref="InvalidOperationException">The room's photo is missing.</exception>
    public static async Task<AnalysisImage> ReadAsync(IFileStorage storage, IRoomImaging imaging, Room room, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(imaging);
        ArgumentNullException.ThrowIfNull(room);

        await using var photo = await storage.OpenReadAsync(room.PhotoKey, cancellationToken)
            ?? throw new InvalidOperationException(Constants.Messages.MissingPhoto(room.Id));

        return imaging.PrepareForAnalysis(photo.Content);
    }
}
