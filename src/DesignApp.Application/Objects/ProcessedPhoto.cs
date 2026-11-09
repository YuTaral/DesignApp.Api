namespace DesignApp.Application.Objects;

/// <summary>A photo ready to store: a JPEG plus its size.</summary>
/// <param name="Content">The encoded JPEG bytes.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
public sealed record ProcessedPhoto(byte[] Content, int Width, int Height);
