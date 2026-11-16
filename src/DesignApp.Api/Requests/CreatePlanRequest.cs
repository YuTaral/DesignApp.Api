using static DesignApp.Domain.Constants;

namespace DesignApp.Api.Requests;

/// <summary>The body of <c>POST /api/plans</c>: a room to build, with its walls.</summary>
/// <param name="Name">Its name; may be empty.</param>
/// <param name="RoomType">What kind of room it is, which decides the common objects offered.</param>
/// <param name="Width">The width, in metres.</param>
/// <param name="Length">The length, in metres.</param>
/// <param name="Height">The ceiling height, in metres.</param>
/// <param name="WallColor">The walls' color as #RRGGBB.</param>
/// <param name="FloorColor">The floor's color as #RRGGBB.</param>
/// <param name="FloorType">What the floor is made of (Wood when missing).</param>
/// <param name="Furnish">Whether it starts with a suggested layout of furniture, windows and a door sized for it (yes when missing).</param>
public sealed record CreatePlanRequest(string? Name, RoomType RoomType, double Width, double Length, double Height, string? WallColor, string? FloorColor,
    FloorType FloorType = FloorType.Wood, bool Furnish = true);
