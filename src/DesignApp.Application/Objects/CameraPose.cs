namespace DesignApp.Application.Objects;

/// <summary>Where the camera stands to look at the wall in view, and how it looks, in centimetres.</summary>
/// <param name="FromLeft">Where it stands from the photo's left wall.</param>
/// <param name="Stand">How far from the wall in view (the far wall) it stands.</param>
/// <param name="Turn">How far it is turned to the right of looking straight at the far wall, in radians.</param>
/// <param name="Fov">Its fixed horizontal view in degrees, or <c>null</c> for one fitted to the room's width.</param>
public sealed record CameraPose(double FromLeft, double Stand, double Turn, double? Fov);
