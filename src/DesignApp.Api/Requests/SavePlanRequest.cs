using static DesignApp.Domain.Constants;

namespace DesignApp.Api.Requests;

/// <summary>The body of <c>PUT /api/plans/{id}</c>: the built room as the user left it.</summary>
/// <param name="Name">Its name; may be empty.</param>
/// <param name="Width">The width, in metres; only used when there is no outline.</param>
/// <param name="Length">The length, in metres; only used when there is no outline.</param>
/// <param name="Height">The ceiling height, in metres.</param>
/// <param name="WallColor">The walls' color as #RRGGBB.</param>
/// <param name="FloorColor">The floor's color as #RRGGBB.</param>
/// <param name="FloorType">What the floor is made of.</param>
/// <param name="View">
/// The number of the wall the 3D view and the photo look at (the camera stands across the room from it); a rectangle's
/// walls are 0 back, 1 right, 2 front and 3 left.
/// </param>
/// <param name="Items">The objects placed in it.</param>
/// <param name="Outline">
/// Its walls' corners, clockwise from the top-left one at 0, 0, in centimetres; when missing the room is a
/// <paramref name="Width"/> × <paramref name="Length"/> rectangle.
/// </param>
/// <param name="Corner">
/// Where the camera stands to look at <paramref name="View"/>: <c>None</c> (across from its middle, when missing), or the
/// near <c>Left</c> or <c>Right</c> corner, looking diagonally across the room, or <c>Spot</c>, at the plan's camera spot.
/// </param>
/// <param name="Finish">How the walls and ceiling are finished and lit; the plan keeps its own when missing.</param>
/// <param name="CameraSpot">
/// Where the photo the plan was read from was taken, in centimetres; the plan keeps its own when missing, and drops it
/// when it is outside the walls.
/// </param>
public sealed record SavePlanRequest(string? Name, double Width, double Length, double Height, string? WallColor, string? FloorColor,
    FloorType FloorType, int View, List<PlanItemRequest>? Items, List<OutlinePointRequest>? Outline = null, ViewCorner Corner = ViewCorner.None,
    RoomFinishRequest? Finish = null, OutlinePointRequest? CameraSpot = null);
