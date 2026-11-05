using DesignApp.Application.Abstractions;
using DesignApp.Application.Analysis;
using DesignApp.Application.Common;
using DesignApp.Domain.Rooms;

namespace DesignApp.Application.Rooms;

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
    /// <summary>Shown when a fix starts from a label map version that isn't stored.</summary>
    public const string UnknownRevision = "This room has changed since you opened it. Reload the page and try again.";

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
            return Result.Failure<Room>("The outlines can be fixed once the analysis is ready.");
        if (!room.HasLabelMapRevision(baseRevision))
            return Result.Failure<Room>(UnknownRevision);
        var target = room.Objects.FirstOrDefault(o => o.Index == objectIndex);
        if (target is null)
            return Result.Failure<Room>("That object isn't in this room.");
        if (points.Count is 0 or > OutlineStrokeRules.MaxPoints)
            return Result.Failure<Room>($"A stroke needs between 1 and {OutlineStrokeRules.MaxPoints} points.");
        if (double.IsNaN(radius) || radius is < OutlineStrokeRules.MinRadius or > OutlineStrokeRules.MaxRadius)
            return Result.Failure<Room>($"The brush radius must be between {OutlineStrokeRules.MinRadius} and {OutlineStrokeRules.MaxRadius} pixels.");
        if (points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y) || p.X is < -1 or > 2 || p.Y is < -1 or > 2))
            return Result.Failure<Room>("The stroke has points far outside the photo.");

        byte[] labelMap;
        await using (var stored = await storage.OpenReadAsync(StorageKeys.RoomLabelMap(room.Id, baseRevision), cancellationToken)
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
            await storage.SaveAsync(StorageKeys.RoomLabelMap(room.Id, revision), content, "image/png", cancellationToken);
        room.AddLabelMapRevision(r => StorageKeys.RoomLabelMap(room.Id, r));
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
            return Result.Failure<Room>(UnknownRevision);
        if (room.LabelMapRevision != revision)
        {
            room.UseLabelMapRevision(revision, r => StorageKeys.RoomLabelMap(room.Id, r));
            await rooms.SaveChangesAsync(cancellationToken);
        }
        return room;
    }
}
