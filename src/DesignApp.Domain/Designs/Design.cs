using DesignApp.Domain.Common;
using DesignApp.Domain.Rooms;

namespace DesignApp.Domain.Designs;

/// <summary>A saved restyling of a room: the original photo plus an ordered list of edits.</summary>
public sealed class Design
{
    public const int MaxNameLength = 80;

    private readonly List<EditOperation> _edits = [];

    public Guid Id { get; private set; }
    public Guid RoomId { get; private set; }
    public string OwnerId { get; private set; } = "";
    public string Name { get; private set; } = "";
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<EditOperation> Edits => _edits;

    private Design() { } // for EF Core

    public static Design Create(Room room, string ownerId, string name, IEnumerable<EditOperation> edits, DateTimeOffset now)
    {
        if (room.Status != RoomStatus.Ready)
            throw new DomainException("Designs can only be created for rooms that finished analysis.");
        if (room.OwnerId != ownerId)
            throw new DomainException("Designs can only be created for your own rooms.");

        var design = new Design { Id = Guid.CreateVersion7(now), RoomId = room.Id, OwnerId = ownerId };
        design.Update(room, name, edits, now);
        return design;
    }

    public void Update(Room room, string name, IEnumerable<EditOperation> edits, DateTimeOffset now)
    {
        if (room.Id != RoomId)
            throw new DomainException("Edits must belong to the design's room.");

        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > MaxNameLength)
            throw new DomainException($"Design name must be 1 to {MaxNameLength} characters.");

        var list = edits.ToList();
        var knownIndexes = room.Objects.Select(o => o.Index).ToHashSet();
        var unknown = list.FirstOrDefault(e => !knownIndexes.Contains(e.ObjectIndex));
        if (unknown is not null)
            throw new DomainException($"Object {unknown.ObjectIndex} does not exist in this room.");

        Name = trimmed;
        _edits.Clear();
        _edits.AddRange(list);
        UpdatedAt = now;
    }
}
