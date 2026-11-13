using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.UseCases;

/// <summary>
/// Merges a part (a chair's seat) back into the object it is a part of: its pixels become the object's again in a new
/// label map version. The part stays listed, so undo (an older version) brings it back.
/// </summary>
/// <param name="rooms">Where rooms are stored.</param>
/// <param name="currentUser">The signed-in user or guest.</param>
/// <param name="storage">Where label maps are stored.</param>
/// <param name="imaging">Changes the label map.</param>
public sealed class MergeRoomPart(IRoomRepository rooms, ICurrentUser currentUser, IFileStorage storage, IRoomImaging imaging)
{
    /// <summary>Merges a part back into its object.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="baseRevision">The label map version the editor shows.</param>
    /// <param name="objectIndex">The part.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The room with its new version, a failure explaining why the part can't be merged, or <c>null</c> when the room isn't found.</returns>
    public async Task<Result<Room>?> MergeAsync(Guid roomId, int baseRevision, int objectIndex, CancellationToken cancellationToken)
    {
        var room = await rooms.FindAsync(roomId, currentUser.Id, cancellationToken);
        if (room is null)
            return null;
        if (room.Status is not RoomStatus.Ready)
            return Result.Failure<Room>(Constants.Messages.OutlinesNotReady);
        if (!room.HasLabelMapRevision(baseRevision))
            return Result.Failure<Room>(Constants.Messages.UnknownRevision);
        var part = room.Objects.FirstOrDefault(o => o.Index == objectIndex);
        if (part is null)
            return Result.Failure<Room>(Constants.Messages.UnknownStrokeObject);
        if (part.ParentIndex is not int parent)
            return Result.Failure<Room>(Constants.Messages.NotAPart);

        byte[] labelMap;
        await using (var stored = await storage.OpenReadAsync(Constants.Rooms.LabelMapKey(room.Id, baseRevision), cancellationToken)
            ?? throw new InvalidOperationException($"Label map {baseRevision} of room {room.Id} is missing from storage."))
        {
            using var buffer = new MemoryStream();
            await stored.Content.CopyToAsync(buffer, cancellationToken);
            labelMap = buffer.ToArray();
        }
        var merged = imaging.MergePart(labelMap, part.Index, parent, part.Label);
        if (!merged.Succeeded)
            return Result.Failure<Room>(merged.Error);

        // The new version is stored before the room points at it, so a failed save leaves only an unused file.
        var revision = room.LatestLabelMapRevision + 1;
        using (var content = new MemoryStream(merged.Value, writable: false))
            await storage.SaveAsync(Constants.Rooms.LabelMapKey(room.Id, revision), content, Constants.ContentTypes.Png, cancellationToken);
        room.MergePart(part.Index, baseRevision, r => Constants.Rooms.LabelMapKey(room.Id, r));
        await rooms.SaveChangesAsync(cancellationToken);
        return room;
    }
}
