using DesignApp.Application.Helpers;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;

namespace DesignApp.Application.UseCases;

/// <summary>
/// Use case: one view of a room's 360-degree photo, as the user looks at it, becomes one of the room's photos to choose
/// from. It is cut out of the same picture as every other view, so no AI is involved and the room looks the same in all.
/// </summary>
/// <param name="rooms">Where rooms and their photos are stored.</param>
/// <param name="currentUser">Whose rooms may be changed.</param>
/// <param name="storage">Where the 360-degree photo is.</param>
/// <param name="imaging">Cuts the view.</param>
/// <param name="takes">Stores the view.</param>
public sealed class SaveRoomPanoramaView(IRoomRepository rooms, ICurrentUser currentUser, IFileStorage storage, IPanoramaImaging imaging, TakeStore takes)
{
    /// <summary>Saves the view.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="view">Which way the view looks, and its lens.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The new photo, a failure the user can read, or <c>null</c> when the user has no such room.</returns>
    public async Task<Result<PhotoTake>?> HandleAsync(Guid roomId, PanoramaView view, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(view);

        var room = await rooms.FindAsync(roomId, currentUser.Id, cancellationToken);

        if (room is null)
        {
            return null;
        }

        if (room.Panorama is null)
        {
            return Result.Failure<PhotoTake>(Constants.Messages.NoPanorama);
        }

        if (!view.IsAllowed)
        {
            return Result.Failure<PhotoTake>(Constants.Messages.PanoramaView);
        }

        await using var file = await storage.OpenReadAsync(room.Panorama.Key, cancellationToken);

        if (file is null)
        {
            return Result.Failure<PhotoTake>(Constants.Messages.NoPanorama);
        }

        using var buffer = new MemoryStream();

        await file.Content.CopyToAsync(buffer, cancellationToken);
        var cut = imaging.Cut(buffer.ToArray(), view, Constants.Panoramas.ViewWidth, Constants.Panoramas.ViewHeight);
        var stored = await takes.StoreAsync(room.Id, cut, Constants.Panoramas.ViewNote(view.Yaw), null, cancellationToken);

        if (!stored.Succeeded)
        {
            return stored;
        }

        rooms.AddTake(stored.Value);
        await rooms.SaveChangesAsync(cancellationToken);

        return stored;
    }
}
