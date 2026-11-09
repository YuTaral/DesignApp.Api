namespace DesignApp.Api.Rooms;

/// <summary>A box in normalized image coordinates (0..1).</summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Width as a fraction of the photo's width.</param>
/// <param name="Height">Height as a fraction of the photo's height.</param>
public sealed record BoundingBoxResponse(double X, double Y, double Width, double Height);
