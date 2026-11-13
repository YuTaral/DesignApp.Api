using DesignApp.Domain.Objects;

namespace DesignApp.Domain.Entities;

/// <summary>One room of a design, with the edits made to it.</summary>
public sealed class DesignRoom
{
    private IReadOnlyList<EditOperation> _edits = [];
    private IReadOnlyList<SurfaceSize> _sizes = [];

    /// <summary>The room.</summary>
    public Guid RoomId { get; private set; }
    /// <summary>Where it is in the design's order (the order rooms were added).</summary>
    public int Position { get; private set; }
    /// <summary>The edits, applied in order on top of the room's original photo.</summary>
    public IReadOnlyList<EditOperation> Edits => _edits;
    /// <summary>The sizes the user gave its walls, floors and ceilings, at most one per object.</summary>
    public IReadOnlyList<SurfaceSize> Sizes => _sizes;

    private DesignRoom() { } // for EF Core

    /// <summary>A room with no edits.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="position">Its place in the design.</param>
    internal DesignRoom(Guid roomId, int position)
    {
        (RoomId, Position) = (roomId, position);
    }

    /// <summary>Replaces the edits; <see cref="Design.SetRoomEdits(Room, IEnumerable{EditOperation}, IEnumerable{SurfaceSize}, DateTimeOffset)"/> checks them first.</summary>
    /// <param name="edits">The new edits.</param>
    internal void SetEdits(IReadOnlyList<EditOperation> edits)
    {
        _edits = edits;
    }

    /// <summary>Replaces the sizes; <see cref="Design.SetRoomEdits(Room, IEnumerable{EditOperation}, IEnumerable{SurfaceSize}, DateTimeOffset)"/> checks them first.</summary>
    /// <param name="sizes">The new sizes.</param>
    internal void SetSizes(IReadOnlyList<SurfaceSize> sizes)
    {
        _sizes = sizes;
    }
}
