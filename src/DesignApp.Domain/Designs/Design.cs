using DesignApp.Domain.Common;
using DesignApp.Domain.Rooms;

namespace DesignApp.Domain.Designs;

/// <summary>A saved restyling of a room: the original photo plus an ordered list of edits.</summary>
public sealed class Design
{
    /// <summary>The longest allowed design name, after trimming.</summary>
    public const int MaxNameLength = 80;

    private IReadOnlyList<EditOperation> _edits = [];

    /// <summary>The design's id.</summary>
    public Guid Id { get; private set; }
    /// <summary>The room this design restyles.</summary>
    public Guid RoomId { get; private set; }
    /// <summary>The user who owns the design.</summary>
    public string OwnerId { get; private set; } = "";
    /// <summary>The name the user gave the design.</summary>
    public string Name { get; private set; } = "";
    /// <summary>When the design was last saved.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }
    /// <summary>The edits, applied in order on top of the original photo.</summary>
    public IReadOnlyList<EditOperation> Edits => _edits;

    private Design() { } // for EF Core

    /// <summary>Creates a design for a room that finished analysis.</summary>
    /// <param name="room">The room to restyle; must be ready and owned by <paramref name="ownerId"/>.</param>
    /// <param name="ownerId">The user creating the design.</param>
    /// <param name="name">The design's name; it is trimmed.</param>
    /// <param name="edits">The edits to store.</param>
    /// <param name="now">The creation time.</param>
    /// <exception cref="DomainException">The room isn't ready or isn't the owner's, or the name or edits are invalid.</exception>
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

    /// <summary>Replaces the design's name and edits.</summary>
    /// <param name="room">The design's room, used to check that every edit targets a real object.</param>
    /// <param name="name">The new name; it is trimmed.</param>
    /// <param name="edits">The new edits; they replace the old ones.</param>
    /// <param name="now">The update time.</param>
    /// <exception cref="DomainException">The room, name or edits are invalid.</exception>
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
        var repeated = list.GroupBy(e => e.ObjectIndex).FirstOrDefault(g => g.Count() > 1);
        if (repeated is not null)
            throw new DomainException($"Object {repeated.Key} has more than one edit.");

        Name = trimmed;
        _edits = list.AsReadOnly();
        UpdatedAt = now;
    }
}
