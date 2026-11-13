namespace DesignApp.Application.Objects;

/// <summary>
/// A measured depth map: a PNG grid in the photo's shape where each cell holds how far that part of the photo is from
/// the camera (16 bits in the red and green channels, relative to the farthest cell), and the camera's field of view.
/// </summary>
/// <param name="Png">The depth map.</param>
/// <param name="FovX">The horizontal field of view, in degrees.</param>
/// <param name="FovY">The vertical field of view, in degrees.</param>
public sealed record DepthMeasurement(byte[] Png, double FovX, double FovY);
