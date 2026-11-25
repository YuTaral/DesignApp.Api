namespace DesignApp.Infrastructure.Photos;

/// <summary>A generated photo and the 3D view it was made from, in Lab, with the region each pixel belongs to.</summary>
/// <param name="Width">Their width in pixels.</param>
/// <param name="Height">Their height in pixels.</param>
/// <param name="Regions">Each pixel's region, row by row from the top: 0 for none, else from 1 up.</param>
/// <param name="Photo">The photo's pixels, three Lab values each.</param>
/// <param name="Guide">The 3D view's pixels, three Lab values each.</param>
internal sealed record PhotoPixels(int Width, int Height, int[] Regions, float[] Photo, float[] Guide);
