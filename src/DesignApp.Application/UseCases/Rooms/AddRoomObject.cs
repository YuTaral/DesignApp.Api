using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Objects;

using static DesignApp.Application.Constants;
using static DesignApp.Domain.Constants;

namespace DesignApp.Application.UseCases;

/// <summary>
/// Use case: the user marks an object the AI missed with a brush, names it and picks its type. The marked
/// pixels become a new object, stored as a new label map version, so the editor's undo can switch back to the
/// version without it.
/// </summary>
/// <param name="rooms">The room database.</param>
/// <param name="currentUser">Whose rooms may be changed.</param>
/// <param name="storage">Where the photo and label maps are stored.</param>
/// <param name="imaging">Does the pixel work.</param>
public sealed class AddRoomObject(IRoomRepository rooms, ICurrentUser currentUser, IFileStorage storage, IRoomImaging imaging)
{
    /// <summary>Adds a hand-marked object, starting from a given label map version.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="baseRevision">The label map version the user marked on.</param>
    /// <param name="label">The object's name.</param>
    /// <param name="category">The object's type.</param>
    /// <param name="strokes">The strokes, applied in order, each with its brush radius.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The room with the new object current, a failure the user can read, or <c>null</c> when the user has no such room.</returns>
    public async Task<Result<Room>?> AddAsync(
        Guid roomId, int baseRevision, string? label, ObjectCategory category, IReadOnlyList<MarkStroke> strokes,
        CancellationToken cancellationToken)
    {
        var room = await rooms.FindAsync(roomId, currentUser.Id, cancellationToken);
        if (room is null)
            return null;
        var invalid = Check(room, baseRevision, label, category, strokes);
        if (invalid is not null)
            return Result.Failure<Room>(invalid);

        var labelMap = await ReadAsync(Constants.Rooms.LabelMapKey(room.Id, baseRevision), cancellationToken);
        Result<MarkedObject> marked;
        await using (var photo = await storage.OpenReadAsync(room.PhotoKey, cancellationToken)
            ?? throw new InvalidOperationException($"The photo of room {room.Id} is missing from storage."))
        {
            marked = imaging.MarkObject(labelMap, photo.Content, new NewObjectMark(room.NextObjectIndex, strokes));
        }
        if (!marked.Succeeded)
            return Result.Failure<Room>(marked.Error);

        // The new version is stored before the room points at it, so a failed save leaves only an unused file.
        var revision = room.LatestLabelMapRevision + 1;
        using (var content = new MemoryStream(marked.Value.LabelMapPng, writable: false))
            await storage.SaveAsync(Constants.Rooms.LabelMapKey(room.Id, revision), content, Constants.ContentTypes.Png, cancellationToken);
        var detected = new DetectedObject(room.NextObjectIndex, label!.Trim(), category, marked.Value.Box, marked.Value.DominantColor);
        room.AddObject(detected, r => Constants.Rooms.LabelMapKey(room.Id, r));
        await rooms.SaveChangesAsync(cancellationToken);
        return room;
    }

    /// <summary>Why the request can't be applied, or <c>null</c> when it can.</summary>
    /// <param name="room">The room.</param>
    /// <param name="baseRevision">The label map version the user marked on.</param>
    /// <param name="label">The object's name.</param>
    /// <param name="category">The object's type.</param>
    /// <param name="strokes">The strokes.</param>
    private static string? Check(Room room, int baseRevision, string? label, ObjectCategory category, IReadOnlyList<MarkStroke> strokes)
    {
        if (room.Status is not RoomStatus.Ready)
            return Constants.Messages.OutlinesNotReady;
        if (!room.HasLabelMapRevision(baseRevision))
            return Constants.Messages.UnknownRevision;
        if (room.NextObjectIndex > Domain.Constants.Rooms.MaxObjectIndex)
            return Constants.Messages.TooManyObjects;
        if (string.IsNullOrWhiteSpace(label) || label.Trim().Length > Constants.Marks.MaxLabelLength)
            return Constants.Messages.ObjectName;
        if (!Enum.IsDefined(category))
            return Constants.Messages.UnknownObjectType;
        if (strokes.Count is 0 or > Constants.Marks.MaxStrokes)
            return Constants.Messages.MarkStrokes;
        if (strokes.Any(s => s.Points is null || s.Points.Count is 0 or > Constants.Strokes.MaxPoints))
            return Constants.Messages.StrokePoints;
        if (strokes.Any(s => double.IsNaN(s.Radius) || s.Radius is < Constants.Strokes.MinRadius or > Constants.Strokes.MaxRadius))
            return Constants.Messages.BrushRadius;
        if (strokes.SelectMany(s => s.Points).Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)
            || p.X is < Constants.Strokes.MinPoint or > Constants.Strokes.MaxPoint || p.Y is < Constants.Strokes.MinPoint or > Constants.Strokes.MaxPoint))
            return Constants.Messages.StrokeOutside;
        return null;
    }

    /// <summary>Reads a stored file into memory.</summary>
    /// <param name="key">The storage key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="InvalidOperationException">The file is missing.</exception>
    private async Task<byte[]> ReadAsync(string key, CancellationToken cancellationToken)
    {
        await using var stored = await storage.OpenReadAsync(key, cancellationToken)
            ?? throw new InvalidOperationException($"The file {key} is missing from storage.");
        using var buffer = new MemoryStream();
        await stored.Content.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}
