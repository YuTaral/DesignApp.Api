using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Entities;

/// <summary>
/// A saved restyling: a name, a short description and one or more rooms, each with its own edits
/// on top of its original photo. A room belongs to at most one design, and a design always has at
/// least one room.
/// </summary>
public sealed class Design
{
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
        return Create(room, ownerId, name, description, edits, [], now);
    }

    /// <summary>Creates a design with its first room and the sizes of its surfaces.</summary>
    /// <param name="room">The first room; it must be the owner's.</param>
    /// <param name="ownerId">The user creating the design.</param>
    /// <param name="name">The design's name; it is trimmed.</param>
    /// <param name="description">An optional short description; it is trimmed.</param>
    /// <param name="edits">The room's edits.</param>
    /// <param name="sizes">The sizes of the room's walls, floors and ceilings.</param>
    /// <param name="now">The creation time.</param>
    /// <exception cref="DomainException">The room isn't the owner's, or the name, description, edits or sizes are invalid.</exception>
    public static Design Create(
        Room room, string ownerId, string name, string? description, IEnumerable<EditOperation> edits, IEnumerable<SurfaceSize> sizes, DateTimeOffset now)
    {
        var design = new Design { Id = Guid.CreateVersion7(now), OwnerId = ownerId, CreatedAt = now };

        design.Rename(name, description, now);
        design.AddRoom(room, now);
        design.SetRoomEdits(room, edits, sizes, now);

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

        if (trimmedName.Length is 0 or > Constants.Designs.MaxNameLength)
        {
            throw new DomainException(Constants.Messages.NameLength);
        }

        var trimmedDescription = (description ?? "").Trim();

        if (trimmedDescription.Length > Constants.Designs.MaxDescriptionLength)
        {
            throw new DomainException(Constants.Messages.DescriptionLength);
        }

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
        {
            throw new DomainException(Constants.Messages.NotYourRoom);
        }

        if (_rooms.Any(r => r.RoomId == room.Id))
        {
            throw new DomainException(Constants.Messages.RoomAlreadyInThisDesign);
        }

        if (_rooms.Count >= Constants.Designs.MaxRooms)
        {
            throw new DomainException(Constants.Messages.DesignFull);
        }

        _rooms.Add(new DesignRoom(room.Id, _rooms.Count == 0 ? 0 : _rooms.Max(r => r.Position) + 1));
        UpdatedAt = now;
    }

    /// <summary>Replaces one room's edits, and clears its sizes.</summary>
    /// <param name="room">The room; it must be in this design, and analyzed when there are edits.</param>
    /// <param name="edits">The new edits, at most one per object.</param>
    /// <param name="now">The change time.</param>
    /// <exception cref="DomainException">The room isn't here or isn't analyzed yet, or an edit is invalid.</exception>
    public void SetRoomEdits(Room room, IEnumerable<EditOperation> edits, DateTimeOffset now)
    {
        SetRoomEdits(room, edits, [], now);
    }

    /// <summary>Replaces one room's edits and the sizes of its surfaces.</summary>
    /// <param name="room">The room; it must be in this design, and analyzed when there are edits or sizes.</param>
    /// <param name="edits">The new edits, at most one per object.</param>
    /// <param name="sizes">The sizes of its walls, floors and ceilings, at most one per object.</param>
    /// <param name="now">The change time.</param>
    /// <exception cref="DomainException">The room isn't here or isn't analyzed yet, or an edit or size is invalid.</exception>
    public void SetRoomEdits(Room room, IEnumerable<EditOperation> edits, IEnumerable<SurfaceSize> sizes, DateTimeOffset now)
    {
        var entry = _rooms.SingleOrDefault(r => r.RoomId == room.Id)
            ?? throw new DomainException(Constants.Messages.RoomNotInDesign);

        var list = edits.ToList();
        var sizeList = sizes.ToList();

        if ((list.Count > 0 || sizeList.Count > 0) && room.Status != RoomStatus.Ready)
        {
            throw new DomainException(Constants.Messages.ColorsNeedAnalysis);
        }

        var knownIndexes = room.Objects.Select(o => o.Index).ToHashSet();
        var unknown = list.FirstOrDefault(e => !knownIndexes.Contains(e.ObjectIndex));

        if (unknown is not null)
        {
            throw new DomainException(Constants.Messages.UnknownObject(unknown.ObjectIndex));
        }

        var repeated = list.GroupBy(e => e.ObjectIndex).FirstOrDefault(g => g.Count() > 1);

        if (repeated is not null)
        {
            throw new DomainException(Constants.Messages.DuplicateEdit(repeated.Key));
        }

        var unknownSize = sizeList.FirstOrDefault(s => !knownIndexes.Contains(s.ObjectIndex));

        if (unknownSize is not null)
        {
            throw new DomainException(Constants.Messages.UnknownSizeObject(unknownSize.ObjectIndex));
        }

        var repeatedSize = sizeList.GroupBy(s => s.ObjectIndex).FirstOrDefault(g => g.Count() > 1);

        if (repeatedSize is not null)
        {
            throw new DomainException(Constants.Messages.DuplicateSize(repeatedSize.Key));
        }

        entry.SetEdits(list.AsReadOnly());
        entry.SetSizes(sizeList.AsReadOnly());
        UpdatedAt = now;
    }

    /// <summary>Takes a room out of the design. The last room can't be removed: delete the design instead.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="now">The change time.</param>
    /// <exception cref="DomainException">The room isn't here, or it is the last one.</exception>
    public void RemoveRoom(Guid roomId, DateTimeOffset now)
    {
        var entry = _rooms.SingleOrDefault(r => r.RoomId == roomId)
            ?? throw new DomainException(Constants.Messages.RoomNotInDesign);

        if (_rooms.Count == 1)
        {
            throw new DomainException(Constants.Messages.LastRoom);
        }

        _rooms.Remove(entry);
        UpdatedAt = now;
    }
}
