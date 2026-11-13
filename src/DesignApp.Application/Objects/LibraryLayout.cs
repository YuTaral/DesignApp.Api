using DesignApp.Domain.Objects;

namespace DesignApp.Application.Objects;

/// <summary>One layout of a library room: a photo of it, analyzed ahead of time.</summary>
/// <param name="Id">The layout's id inside its room, such as <c>2</c>.</param>
/// <param name="Width">Width of the photo, in pixels.</param>
/// <param name="Height">Height of the photo, in pixels.</param>
/// <param name="Objects">The objects in its label map, ordered by index.</param>
/// <param name="DepthFovX">Its depth map's horizontal field of view in degrees, or <c>null</c> when it has no depth map.</param>
/// <param name="DepthFovY">Its depth map's vertical field of view in degrees, or <c>null</c> when it has no depth map.</param>
/// <param name="HasBackdrop">Whether it has an empty-room backdrop for the 3D view.</param>
public sealed record LibraryLayout(
    string Id, int Width, int Height, IReadOnlyList<DetectedObject> Objects, double? DepthFovX = null, double? DepthFovY = null, bool HasBackdrop = false);
