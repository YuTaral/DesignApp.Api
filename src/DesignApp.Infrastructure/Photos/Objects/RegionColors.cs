namespace DesignApp.Infrastructure.Photos;

/// <summary>One region of a photo, measured: where it is and its typical color in the photo and in the 3D view.</summary>
/// <param name="Region">The region.</param>
/// <param name="Left">Its box's left edge in the picture (the box reaches past the region by the blur's reach).</param>
/// <param name="Top">Its box's top edge.</param>
/// <param name="Width">Its box's width.</param>
/// <param name="Height">Its box's height.</param>
/// <param name="Member">1 where the box's pixel is in the region, else 0.</param>
/// <param name="Size">How many pixels its typical colors were measured on.</param>
/// <param name="Photo">Its typical color in the photo, three Lab values.</param>
/// <param name="Guide">Its typical color in the 3D view, over the same pixels.</param>
internal sealed record RegionColors(int Region, int Left, int Top, int Width, int Height, float[] Member, int Size, float[] Photo, float[] Guide);
