using DesignApp.Application.Helpers;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Extensions;

using static DesignApp.Application.Constants;
using static DesignApp.Domain.Constants;

namespace DesignApp.Application.UseCases;

/// <summary>
/// Use case: the user removes objects from the photo, one or several together (a sofa with the cushions and throw on
/// it, so nothing is left floating). The room's AI fills their pixels with what is behind them in one call; the new
/// photo and a label map where those pixels belong to the objects around them are stored as a new version, so the
/// editor's undo can switch back to the photo with them.
/// </summary>
/// <param name="rooms">The room database.</param>
/// <param name="currentUser">Whose rooms may be changed.</param>
/// <param name="storage">Where the photos and label maps are stored.</param>
/// <param name="imaging">Does the pixel work.</param>
/// <param name="ai">The room's AI, which does the removal.</param>
public sealed class RemoveRoomObject(IRoomRepository rooms, ICurrentUser currentUser, IFileStorage storage, IRoomImaging imaging, IAiProviders ai)
{
    /// <summary>Removes objects from the photo together, starting from a given label map version.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="baseRevision">The label map version the editor shows.</param>
    /// <param name="objectIndexes">The objects to remove.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The room with its new current version, a failure the user can read, or <c>null</c> when the user has no such room.</returns>
    public async Task<Result<Room>?> RemoveAsync(Guid roomId, int baseRevision, IReadOnlyList<int> objectIndexes, CancellationToken cancellationToken)
    {
        var room = await rooms.FindAsync(roomId, currentUser.Id, cancellationToken);

        if (room is null)
        {
            return null;
        }

        if (RoomEditing.Refusal(room, baseRevision) is { } refusal)
        {
            return Result.Failure<Room>(refusal);
        }

        var indexes = objectIndexes.Distinct().ToList();

        if (indexes.Count is 0 or > Constants.Removals.MaxObjects)
        {
            return Result.Failure<Room>(Constants.Messages.RemovalCount);
        }

        var targets = indexes.Select(i => room.Objects.FirstOrDefault(o => o.Index == i)).ToList();

        if (targets.Any(t => t is null))
        {
            return Result.Failure<Room>(Constants.Messages.UnknownStrokeObject);
        }

        if (targets.Any(t => t!.Category.IsSurface()))
        {
            return Result.Failure<Room>(Constants.Messages.SurfaceRemoval);
        }

        if (targets.FirstOrDefault(t => t!.ParentIndex is not null) is { } part)
        {
            return Result.Failure<Room>(Constants.Messages.PartRemoval(part.Label, room.Objects.First(o => o.Index == part.ParentIndex).Label));
        }

        // An object goes together with its parts.
        indexes = [.. indexes, .. room.Objects.Where(o => o.ParentIndex is int p && indexes.Contains(p)).Select(o => o.Index)];

        var labelMap = await storage.ReadAllBytesAsync(Constants.Rooms.LabelMapKey(room.Id, baseRevision), cancellationToken);
        var mask = imaging.RemovalMask(labelMap, indexes, room.Width, room.Height);

        if (mask is null)
        {
            return Result.Failure<Room>(Constants.Messages.ObjectNotInPhoto);
        }

        var photo = await storage.ReadAllBytesAsync(Constants.Rooms.PhotoKey(room.Id, room.PhotoVersionOf(baseRevision)), cancellationToken);

        // The mask counts as brushed only when every object in it was marked by hand.
        var brushed = targets.All(t => t!.MarkedByHand);
        var filled = await ai.Removal(room.AiMode).RemoveAsync(photo, mask, brushed, cancellationToken);

        if (!filled.Succeeded)
        {
            return Result.Failure<Room>(filled.Error);
        }

        var removed = imaging.AfterRemoval(labelMap, indexes, filled.Value, room.Width, room.Height);

        // The new files are stored before the room points at them, so a failed save leaves only unused files.
        var (revision, photoVersion) = (room.LatestLabelMapRevision + 1, room.LatestPhotoVersion + 1);

        using (var content = new MemoryStream(removed.PhotoJpeg, writable: false))
            await storage.SaveAsync(Constants.Rooms.PhotoKey(room.Id, photoVersion), content, Constants.ContentTypes.Jpeg, cancellationToken);

        using (var content = new MemoryStream(removed.LabelMapPng, writable: false))
            await storage.SaveAsync(Constants.Rooms.LabelMapKey(room.Id, revision), content, Constants.ContentTypes.Png, cancellationToken);

        room.RemoveObjects(indexes, baseRevision, r => Constants.Rooms.LabelMapKey(room.Id, r));
        await rooms.SaveChangesAsync(cancellationToken);

        return room;
    }
}
