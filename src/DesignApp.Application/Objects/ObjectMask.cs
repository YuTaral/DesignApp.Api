namespace DesignApp.Application.Objects;

/// <summary>
/// The outline of one object: how much each pixel belongs to it, from 0 (not at all) to 255 (fully).
/// It may be smaller or larger than the analysis image; it is scaled to fit.
/// </summary>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="Coverage">One byte per pixel, row by row.</param>
public sealed record ObjectMask(int Width, int Height, byte[] Coverage);
