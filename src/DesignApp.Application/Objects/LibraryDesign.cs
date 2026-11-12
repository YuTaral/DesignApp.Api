namespace DesignApp.Application.Objects;

/// <summary>A built-in library design: one house in one style, with every room ready to restyle.</summary>
/// <param name="Id">The design's id, such as <c>minimalist</c>.</param>
/// <param name="Name">The name people see, such as "Minimalist house".</param>
/// <param name="Style">The style, such as "Minimalist".</param>
/// <param name="Description">A sentence about the house.</param>
/// <param name="Rooms">The rooms, in the order they are shown.</param>
public sealed record LibraryDesign(string Id, string Name, string Style, string Description, IReadOnlyList<LibraryRoom> Rooms)
{
    /// <summary>The room with this id, or <c>null</c>.</summary>
    /// <param name="roomId">The room's id inside the design.</param>
    public LibraryRoom? FindRoom(string roomId)
    {
        return Rooms.FirstOrDefault(r => r.Id == roomId);
    }
}
