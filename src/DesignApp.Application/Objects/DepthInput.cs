using DesignApp.Domain.Objects;

namespace DesignApp.Application.Objects;

/// <summary>What a depth provider measures: a room's original photo, with its analysis for providers that need it.</summary>
/// <param name="Photo">The photo, as a JPEG.</param>
/// <param name="Width">Width of the photo, in pixels.</param>
/// <param name="Height">Height of the photo, in pixels.</param>
/// <param name="LabelMap">The analysis' label map (a PNG of object indexes).</param>
/// <param name="Objects">The objects in the label map.</param>
public sealed record DepthInput(byte[] Photo, int Width, int Height, byte[] LabelMap, IReadOnlyList<DetectedObject> Objects);
