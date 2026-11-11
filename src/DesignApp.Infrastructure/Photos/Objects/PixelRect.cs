namespace DesignApp.Infrastructure.Photos;

/// <summary>A rectangle in a label map's pixels: from (<paramref name="X0"/>, <paramref name="Y0"/>) up to, not including, (<paramref name="X1"/>, <paramref name="Y1"/>).</summary>
/// <param name="X0">The left column.</param>
/// <param name="Y0">The top row.</param>
/// <param name="X1">The column just past the right edge.</param>
/// <param name="Y1">The row just past the bottom edge.</param>
internal readonly record struct PixelRect(int X0, int Y0, int X1, int Y1)
{
    /// <summary>Whether the pixel at (<paramref name="x"/>, <paramref name="y"/>) is inside.</summary>
    /// <param name="x">Its column.</param>
    /// <param name="y">Its row.</param>
    public bool Contains(int x, int y)
    {
        return x >= X0 && x < X1 && y >= Y0 && y < Y1;
    }
}
