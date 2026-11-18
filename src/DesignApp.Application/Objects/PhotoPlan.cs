using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.Objects;

/// <summary>
/// A room photo read into a plan by the AI: the room's type, size and surfaces, where the photo was taken from (it looks
/// at the back wall), and its objects as catalog objects placed on the plan, as the AI judged them.
/// </summary>
/// <param name="RoomType">What kind of room it is.</param>
/// <param name="Width">From the left wall to the right wall, in metres.</param>
/// <param name="Length">From the back wall to the front wall, in metres.</param>
/// <param name="Height">The ceiling height, in metres.</param>
/// <param name="WallColor">The walls' color, #RRGGBB (may be invalid).</param>
/// <param name="Walls">What the walls are finished in.</param>
/// <param name="FloorColor">The floor's color, #RRGGBB (may be invalid).</param>
/// <param name="FloorType">What the floor is made of.</param>
/// <param name="CeilingLight">The light on the ceiling.</param>
/// <param name="Camera">Where the photo was taken from on the plan, in centimetres (it may be outside the room).</param>
/// <param name="Objects">The objects.</param>
public sealed record PhotoPlan(RoomType RoomType, double Width, double Length, double Height, string WallColor, WallFinish Walls, string FloorColor,
    FloorType FloorType, CeilingLight CeilingLight, OutlinePoint Camera, IReadOnlyList<PhotoPlanItem> Objects);
