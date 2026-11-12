namespace DesignApp.Infrastructure.Library;

/// <summary>A library design's <c>design.json</c>.</summary>
/// <param name="Id">The design's id; it must match its folder's name.</param>
/// <param name="Name">The name people see.</param>
/// <param name="Style">The style.</param>
/// <param name="Description">A sentence about the house.</param>
/// <param name="Order">Where it is listed among the designs (lowest first).</param>
/// <param name="Rooms">The rooms, in the order they are shown.</param>
internal sealed record LibraryDesignFile(string Id, string Name, string Style, string Description, int Order, IReadOnlyList<LibraryRoomEntry> Rooms);
