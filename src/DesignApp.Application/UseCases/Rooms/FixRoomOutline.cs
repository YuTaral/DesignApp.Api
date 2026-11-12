using DesignApp.Application.Helpers;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;

using static DesignApp.Application.Constants;
using static DesignApp.Domain.Constants;

namespace DesignApp.Application.UseCases;

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
    /// <summary>Paints or erases an object's outline with a brush, box or lasso stroke, starting from a given label map version.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="baseRevision">The label map version the user drew on.</param>
    /// <param name="objectIndex">The object to change.</param>
    /// <param name="mode">Paint or erase.</param>
    /// <param name="radius">The brush radius, in label map pixels.</param>
    /// <param name="points">The stroke's path, 0..1 of the photo.</param>
    /// <param name="shape">What the stroke covers: the brush along the path, a box, or the inside of a lasso.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The room with its new current version, a failure the user can read, or <c>null</c> when the user has no such room.</returns>
    public async Task<Result<Room>?> FixAsync(
        Guid roomId, int baseRevision, int objectIndex, OutlineBrushMode mode, double radius, IReadOnlyList<StrokePoint> points,
        StrokeShape shape, CancellationToken cancellationToken)
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
        var badShape = StrokeShapes.Check(shape, points);
        if (badShape is not null)
            return Result.Failure<Room>(badShape);

        byte[] labelMap;
        await using (var stored = await storage.OpenReadAsync(Constants.Rooms.LabelMapKey(room.Id, baseRevision), cancellationToken)
            ?? throw new InvalidOperationException($"Label map {baseRevision} of room {room.Id} is missing from storage."))
        {
            using var buffer = new MemoryStream();
            await stored.Content.CopyToAsync(buffer, cancellationToken);
            labelMap = buffer.ToArray();
        }

        var painted = imaging.PaintOutline(labelMap, new OutlineStroke(target.Index, target.Label, mode, radius, points, shape));
        if (!painted.Succeeded)
            return Result.Failure<Room>(painted.Error);

        // The new version is stored before the room points at it, so a failed save leaves only an unused file.
        var revision = room.LatestLabelMapRevision + 1;
        using (var content = new MemoryStream(painted.Value, writable: false))
            await storage.SaveAsync(Constants.Rooms.LabelMapKey(room.Id, revision), content, Constants.ContentTypes.Png, cancellationToken);
        room.AddLabelMapRevision(baseRevision, r => Constants.Rooms.LabelMapKey(room.Id, r));
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
