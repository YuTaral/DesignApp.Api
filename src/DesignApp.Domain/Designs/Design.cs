using DesignApp.Domain.Common;
using DesignApp.Domain.Rooms;

namespace DesignApp.Domain.Designs;

/// <summary>
/// A saved restyling: a name, a short description and one or more rooms, each with its own edits
/// on top of its original photo. A room belongs to at most one design, and a design always has at
/// least one room.
/// </summary>
public sealed class Design
{
    /// <summary>The longest allowed design name, after trimming.</summary>
    public const int MaxNameLength = 80;

    /// <summary>The longest allowed description, after trimming.</summary>
    public const int MaxDescriptionLength = 300;

    /// <summary>The most rooms one design can hold.</summary>
    public const int MaxRooms = 20;

    private readonly List<DesignRoom> _rooms = [];

    /// <summary>The design's id.</summary>
    public Guid Id { get; private set; }
    /// <summary>The user who owns the design.</summary>
    public string OwnerId { get; private set; } = "";
    /// <summary>The name the user gave the design.</summary>
    public string Name { get; private set; } = "";
    /// <summary>A short description; empty when the user gave none.</summary>
    public string Description { get; private set; } = "";
    /// <summary>When the design was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }
    /// <summary>When the design or one of its rooms was last changed.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }
    /// <summary>The rooms, in the order they were added.</summary>
    public IReadOnlyList<DesignRoom> Rooms => _rooms;

    private Design() { } // for EF Core

    /// <summary>Creates a design with its first room.</summary>
    /// <param name="room">The first room; it must be the owner's.</param>
    /// <param name="ownerId">The user creating the design.</param>
    /// <param name="name">The design's name; it is trimmed.</param>
    /// <param name="description">An optional short description; it is trimmed.</param>
    /// <param name="edits">The room's edits.</param>
    /// <param name="now">The creation time.</param>
    /// <exception cref="DomainException">The room isn't the owner's, or the name, description or edits are invalid.</exception>
    public static Design Create(Room room, string ownerId, string name, string? description, IEnumerable<EditOperation> edits, DateTimeOffset now)
    {
        var design = new Design { Id = Guid.CreateVersion7(now), OwnerId = ownerId, CreatedAt = now };
        design.Rename(name, description, now);
        design.AddRoom(room, now);
        design.SetRoomEdits(room, edits, now);
        return design;
    }

    /// <summary>Changes the name and description.</summary>
    /// <param name="name">The new name; it is trimmed.</param>
    /// <param name="description">The new description, or null for none; it is trimmed.</param>
    /// <param name="now">The change time.</param>
    /// <exception cref="DomainException">The name or description is too long, or the name is empty.</exception>
    public void Rename(string name, string? description, DateTimeOffset now)
    {
        var trimmedName = name.Trim();
        if (trimmedName.Length is 0 or > MaxNameLength)
            throw new DomainException($"Design name must be 1 to {MaxNameLength} characters.");
        var trimmedDescription = (description ?? "").Trim();
        if (trimmedDescription.Length > MaxDescriptionLength)
            throw new DomainException($"The description can be at most {MaxDescriptionLength} characters.");

        Name = trimmedName;
        Description = trimmedDescription;
        UpdatedAt = now;
    }

    /// <summary>Adds a room, with no edits yet. It may still be analyzed.</summary>
    /// <param name="room">The room; it must be the owner's and not in this design already.</param>
    /// <param name="now">The change time.</param>
    /// <exception cref="DomainException">The room isn't the owner's, is already here, or the design is full.</exception>
    public void AddRoom(Room room, DateTimeOffset now)
    {
        if (room.OwnerId != OwnerId)
            throw new DomainException("Designs can only hold your own rooms.");
        if (_rooms.Any(r => r.RoomId == room.Id))
            throw new DomainException("This room is already in the design.");
        if (_rooms.Count >= MaxRooms)
            throw new DomainException($"A design can hold at most {MaxRooms} rooms.");

        _rooms.Add(new DesignRoom(room.Id, _rooms.Count == 0 ? 0 : _rooms.Max(r => r.Position) + 1));
        UpdatedAt = now;
    }

    /// <summary>Replaces one room's edits.</summary>
    /// <param name="room">The room; it must be in this design, and analyzed when there are edits.</param>
    /// <param name="edits">The new edits, at most one per object.</param>
    /// <param name="now">The change time.</param>
    /// <exception cref="DomainException">The room isn't here or isn't analyzed yet, or an edit is invalid.</exception>
    public void SetRoomEdits(Room room, IEnumerable<EditOperation> edits, DateTimeOffset now)
    {
        var entry = _rooms.SingleOrDefault(r => r.RoomId == room.Id)
            ?? throw new DomainException("This room isn't in the design.");

        var list = edits.ToList();
        if (list.Count > 0 && room.Status != RoomStatus.Ready)
            throw new DomainException("Colors can only be saved for rooms that finished analysis.");
        var knownIndexes = room.Objects.Select(o => o.Index).ToHashSet();
        var unknown = list.FirstOrDefault(e => !knownIndexes.Contains(e.ObjectIndex));
        if (unknown is not null)
            throw new DomainException($"Object {unknown.ObjectIndex} does not exist in this room.");
        var repeated = list.GroupBy(e => e.ObjectIndex).FirstOrDefault(g => g.Count() > 1);
        if (repeated is not null)
            throw new DomainException($"Object {repeated.Key} has more than one edit.");

        entry.SetEdits(list.AsReadOnly());
        UpdatedAt = now;
    }

    /// <summary>Takes a room out of the design. The last room can't be removed: delete the design instead.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="now">The change time.</param>
    /// <exception cref="DomainException">The room isn't here, or it is the last one.</exception>
    public void RemoveRoom(Guid roomId, DateTimeOffset now)
    {
        var entry = _rooms.SingleOrDefault(r => r.RoomId == roomId)
            ?? throw new DomainException("This room isn't in the design.");
        if (_rooms.Count == 1)
            throw new DomainException("A design needs at least one room. Delete the design instead.");

        _rooms.Remove(entry);
        UpdatedAt = now;
    }
}

/// <summary>One room of a design, with the edits made to it.</summary>
public sealed class DesignRoom
{
    private IReadOnlyList<EditOperation> _edits = [];

    /// <summary>The room.</summary>
    public Guid RoomId { get; private set; }
    /// <summary>Where it is in the design's order (the order rooms were added).</summary>
    public int Position { get; private set; }
    /// <summary>The edits, applied in order on top of the room's original photo.</summary>
    public IReadOnlyList<EditOperation> Edits => _edits;

    private DesignRoom() { } // for EF Core

    /// <summary>A room with no edits.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="position">Its place in the design.</param>
    internal DesignRoom(Guid roomId, int position) => (RoomId, Position) = (roomId, position);

    /// <summary>Replaces the edits; <see cref="Design.SetRoomEdits"/> checks them first.</summary>
    /// <param name="edits">The new edits.</param>
    internal void SetEdits(IReadOnlyList<EditOperation> edits) => _edits = edits;
}
