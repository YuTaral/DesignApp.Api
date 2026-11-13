using DesignApp.Domain.Objects;

namespace DesignApp.Application.Objects;

/// <summary>A room as it is now, to be packed as a library layout (development only).</summary>
/// <param name="RoomId">The name its files get: the layout's id, such as <c>2</c>.</param>
/// <param name="Photo">The current photo version, as a JPEG.</param>
/// <param name="LabelMap">The current label map version, as a PNG.</param>
/// <param name="Width">Width of the photo, in pixels.</param>
/// <param name="Height">Height of the photo, in pixels.</param>
/// <param name="Objects">The room's objects; ones without pixels in the label map are left out when packed.</param>
/// <param name="Depth">The room's depth map, or <c>null</c> when it has none.</param>
/// <param name="DepthFovX">The depth map's horizontal field of view, in degrees.</param>
/// <param name="DepthFovY">The depth map's vertical field of view, in degrees.</param>
public sealed record LibraryExport(
    string RoomId, byte[] Photo, byte[] LabelMap, int Width, int Height, IReadOnlyList<DetectedObject> Objects,
    byte[]? Depth = null, double? DepthFovX = null, double? DepthFovY = null);
