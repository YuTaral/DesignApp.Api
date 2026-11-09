namespace DesignApp.Application.Objects;

/// <summary>The copy of a room photo that the AI providers see.</summary>
/// <param name="Jpeg">The image as JPEG bytes.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
public sealed record AnalysisImage(byte[] Jpeg, int Width, int Height);
