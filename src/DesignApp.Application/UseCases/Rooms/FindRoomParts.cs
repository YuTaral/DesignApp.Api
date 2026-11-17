using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.UseCases;

/// <summary>
/// Finds the parts of an object with the room's AI, such as a chair's seat, backrest and legs: the inventory model lists
/// them, the segmentation outlines them, and the object's pixels they cover become parts of it in a new label map
/// version. Each part can then be recolored on its own; undo (an older version) takes them away again.
/// </summary>
/// <param name="rooms">Where rooms are stored.</param>
/// <param name="currentUser">The signed-in user or guest.</param>
/// <param name="storage">Where photos and label maps are stored.</param>
/// <param name="imaging">Prepares the photo and changes the label map.</param>
/// <param name="ai">The room's AI.</param>
public sealed class FindRoomParts(IRoomRepository rooms, ICurrentUser currentUser, IFileStorage storage, IRoomImaging imaging, IAiProviders ai)
{
    /// <summary>Finds an object's parts.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="baseRevision">The label map version the editor shows.</param>
    /// <param name="objectIndex">The object.</param>
    /// <param name="instructions">The user's own words for the AI on which parts to find, or <c>null</c>.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The room with the parts current, a failure the user can read, or <c>null</c> when the room isn't found.</returns>
    public async Task<Result<Room>?> FindAsync(Guid roomId, int baseRevision, int objectIndex, string? instructions, CancellationToken cancellationToken)
    {
        var room = await rooms.FindAsync(roomId, currentUser.Id, cancellationToken);

        if (room is null)
        {
            return null;
        }

        if (room.Status is not RoomStatus.Ready)
        {
            return Result.Failure<Room>(Constants.Messages.OutlinesNotReady);
        }

        instructions = string.IsNullOrWhiteSpace(instructions) ? null : instructions.Trim();

        if (instructions?.Length > Constants.Parts.MaxInstructionsLength)
        {
            return Result.Failure<Room>(Constants.Messages.PartsInstructions);
        }

        if (!room.HasLabelMapRevision(baseRevision))
        {
            return Result.Failure<Room>(Constants.Messages.UnknownRevision);
        }

        var whole = room.Objects.FirstOrDefault(o => o.Index == objectIndex);

        if (whole is null)
        {
            return Result.Failure<Room>(Constants.Messages.UnknownStrokeObject);
        }

        if (whole.ParentIndex is int parent)
        {
            return Result.Failure<Room>(Constants.Messages.PartsOfPart(room.Objects.First(o => o.Index == parent).Label));
        }

        if (room.NextObjectIndex > Domain.Constants.Rooms.MaxObjectIndex)
        {
            return Result.Failure<Room>(Constants.Messages.TooManyObjects);
        }

        var photo = await ReadAsync(Constants.Rooms.PhotoKey(room.Id, room.PhotoVersionOf(baseRevision)), cancellationToken);
        AnalysisImage image;

        using (var content = new MemoryStream(photo, writable: false))
            image = imaging.PrepareForAnalysis(content);

        var listed = await ai.Inventory(room.AiMode).ListPartsAsync(image, whole, instructions, cancellationToken);

        if (!listed.Succeeded)
        {
            return Result.Failure<Room>(listed.Error);
        }

        // A room holds at most 255 objects; the parts that don't fit are left out.
        var free = Domain.Constants.Rooms.MaxObjectIndex - room.NextObjectIndex + 1;
        var items = listed.Value.Take(Math.Min(Constants.Parts.MaxParts, free)).ToList();

        if (items.Count == 0)
        {
            return Result.Failure<Room>(Constants.Messages.NoParts(whole.Label));
        }

        var masks = await ai.Segmentation(room.AiMode).SegmentAsync(image, items, cancellationToken);
        var outlined = items.Zip(masks).Where(pair => pair.Second is not null).Select(pair => new SegmentedObject(pair.First, pair.Second!)).ToList();

        if (outlined.Count == 0)
        {
            return Result.Failure<Room>(Constants.Messages.NoParts(whole.Label));
        }

        var labelMap = await ReadAsync(Constants.Rooms.LabelMapKey(room.Id, baseRevision), cancellationToken);
        Result<LabelMap> added;

        using (var content = new MemoryStream(photo, writable: false))
            added = imaging.AddParts(labelMap, content, whole, room.NextObjectIndex, outlined);

        if (!added.Succeeded)
        {
            return Result.Failure<Room>(added.Error);
        }

        // The new version is stored before the room points at it, so a failed save leaves only an unused file.
        var revision = room.LatestLabelMapRevision + 1;

        using (var content = new MemoryStream(added.Value.Png, writable: false))
            await storage.SaveAsync(Constants.Rooms.LabelMapKey(room.Id, revision), content, Constants.ContentTypes.Png, cancellationToken);

        room.AddParts(added.Value.Objects, baseRevision, r => Constants.Rooms.LabelMapKey(room.Id, r));
        await rooms.SaveChangesAsync(cancellationToken);

        return room;
    }

    /// <summary>Reads a stored file.</summary>
    /// <param name="key">Its storage key.</param>
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
