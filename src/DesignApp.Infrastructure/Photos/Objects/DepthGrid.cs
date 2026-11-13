using System.Numerics;
using SkiaSharp;

namespace DesignApp.Infrastructure.Photos;

/// <summary>
/// The depth map of the 3D view: a small grid in the photo's shape, each cell how far that part of the photo is from
/// the camera. Stored as a PNG with 16 bits per cell in the red (high byte) and green (low byte) channels, relative to
/// the farthest cell, because only the room's shape matters, not its size in meters.
/// </summary>
internal static class DepthGrid
{
    /// <summary>The grid's height for a photo: the grid's width scaled to the photo's shape.</summary>
    /// <param name="photoWidth">Width of the photo.</param>
    /// <param name="photoHeight">Height of the photo.</param>
    public static int HeightFor(int photoWidth, int photoHeight)
    {
        return Math.Max(1, (int)Math.Round((double)Constants.Ai.Depth.GridWidth * photoHeight / photoWidth));
    }

    /// <summary>
    /// Puts camera-style points (x right, y down, z forward) into a grid through the camera matrix, keeping the nearest
    /// point per cell. Cells no point falls in stay 0.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <param name="fx">Focal length across, as a fraction of the image's width.</param>
    /// <param name="fy">Focal length down, as a fraction of the image's height.</param>
    /// <param name="cx">Principal point across, as a fraction of the width.</param>
    /// <param name="cy">Principal point down, as a fraction of the height.</param>
    /// <param name="width">Grid width.</param>
    /// <param name="height">Grid height.</param>
    /// <returns>The depth of each cell, row by row.</returns>
    public static float[] Rasterize(ReadOnlySpan<Vector3> points, double fx, double fy, double cx, double cy, int width, int height)
    {
        var grid = new float[width * height];
        foreach (var point in points)
        {
            if (!(point.Z > 0) || !float.IsFinite(point.X) || !float.IsFinite(point.Y))
                continue;
            var u = fx * point.X / point.Z + cx;
            var v = fy * point.Y / point.Z + cy;
            if (u is < 0 or >= 1 || v is < 0 or >= 1)
                continue;
            var cell = (int)(v * height) * width + (int)(u * width);
            if (grid[cell] == 0 || point.Z < grid[cell])
                grid[cell] = point.Z;
        }
        return grid;
    }

    /// <summary>Fills empty cells (0) with the average of their filled neighbors, pass by pass.</summary>
    /// <param name="grid">The grid, changed in place.</param>
    /// <param name="width">Grid width.</param>
    /// <param name="height">Grid height.</param>
    /// <returns>Whether every cell is filled.</returns>
    public static bool FillHoles(float[] grid, int width, int height)
    {
        for (var pass = 0; pass < Constants.Ai.Depth.MaxFillPasses; pass++)
        {
            var next = (float[])grid.Clone();
            var empty = 0;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (grid[y * width + x] > 0)
                        continue;
                    float sum = 0;
                    var count = 0;
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            var (nx, ny) = (x + dx, y + dy);
                            if (nx < 0 || ny < 0 || nx >= width || ny >= height || grid[ny * width + nx] <= 0)
                                continue;
                            sum += grid[ny * width + nx];
                            count++;
                        }
                    }
                    if (count > 0)
                        next[y * width + x] = sum / count;
                    else
                        empty++;
                }
            }
            Array.Copy(next, grid, grid.Length);
            if (empty == 0)
                return true;
        }
        return Array.TrueForAll(grid, d => d > 0);
    }

    /// <summary>Encodes a filled grid as the depth map PNG.</summary>
    /// <param name="grid">The depth of each cell, all above 0.</param>
    /// <param name="width">Grid width.</param>
    /// <param name="height">Grid height.</param>
    public static byte[] ToPng(float[] grid, int width, int height)
    {
        var far = grid.Max();
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        var pixels = bitmap.GetPixelSpan();
        for (var i = 0; i < grid.Length; i++)
        {
            var value = (ushort)Math.Clamp(Math.Round(grid[i] / far * ushort.MaxValue), 1, ushort.MaxValue);
            pixels[i * 4] = (byte)(value >> 8);
            pixels[i * 4 + 1] = (byte)value;
            pixels[i * 4 + 2] = 0;
            pixels[i * 4 + 3] = byte.MaxValue;
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    /// <summary>Reads a depth map PNG back: each cell's depth relative to the farthest (0 to 1).</summary>
    /// <param name="png">The depth map.</param>
    /// <returns>The depths, row by row, and the grid's size.</returns>
    public static (float[] Depth, int Width, int Height) FromPng(byte[] png)
    {
        using var codec = SKCodec.Create(new MemoryStream(png)) ?? throw new InvalidOperationException("The depth map can't be decoded.");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = SKBitmap.Decode(codec, info) ?? throw new InvalidOperationException("The depth map can't be decoded.");
        var pixels = bitmap.GetPixelSpan();
        var depth = new float[info.Width * info.Height];
        for (var i = 0; i < depth.Length; i++)
            depth[i] = ((pixels[i * 4] << 8) | pixels[i * 4 + 1]) / (float)ushort.MaxValue;
        return (depth, info.Width, info.Height);
    }
}
