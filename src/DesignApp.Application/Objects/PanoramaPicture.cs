namespace DesignApp.Application.Objects;

/// <summary>A 360-degree photo ready to store: a JPEG twice as wide as it is tall.</summary>
/// <param name="Content">The JPEG.</param>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels.</param>
public sealed record PanoramaPicture(byte[] Content, int Width, int Height);
