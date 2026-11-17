using SkiaSharp;

namespace DesignApp.Infrastructure.Photos;

/// <summary>
/// Grows a black-and-white mask outward by a number of pixels. An eraser given a hole the exact shape of a small
/// object tends to paint something of that shape back in; a hole with a wider, rounder edge reads as background.
/// </summary>
internal static class MaskGrowth
{
    /// <summary>Grows the white area of a mask PNG by <paramref name="pixels"/> in every direction (a square reach).</summary>
    /// <param name="maskPng">The mask: white marks what to remove.</param>
    /// <param name="pixels">How far to grow; 0 returns the mask as it is.</param>
    /// <returns>The grown mask as a PNG.</returns>
    /// <exception cref="InvalidOperationException">The mask can't be decoded.</exception>
    public static byte[] Grow(byte[] maskPng, int pixels)
    {
        if (pixels <= 0)
        {
            return maskPng;
        }

        using var codec = SKCodec.Create(new MemoryStream(maskPng)) ?? throw new InvalidOperationException("The mask can't be decoded.");
        var (width, height) = (codec.Info.Width, codec.Info.Height);
        using var bitmap = SKBitmap.Decode(codec, new SKImageInfo(width, height, SKColorType.Gray8, SKAlphaType.Opaque))
            ?? throw new InvalidOperationException("The mask can't be decoded.");
        var mask = bitmap.GetPixelSpan().ToArray();

        // A square reach is two passes of a running maximum: along each row, then down each column.
        var rows = new byte[mask.Length];

        for (var y = 0; y < height; y++)
        {
            Spread(mask, rows, y * width, 1, width, pixels);
        }

        var grown = new byte[mask.Length];

        for (var x = 0; x < width; x++)
        {
            Spread(rows, grown, x, width, height, pixels);
        }

        using var result = new SKBitmap(new SKImageInfo(width, height, SKColorType.Gray8, SKAlphaType.Opaque));

        grown.CopyTo(result.GetPixelSpan());
        using var image = SKImage.FromBitmap(result);
        using var png = image.Encode(SKEncodedImageFormat.Png, Constants.Imaging.PngQuality);

        return png.ToArray();
    }

    /// <summary>Along one line, each pixel takes the brightest value within <paramref name="reach"/> of it.</summary>
    /// <param name="from">The values read.</param>
    /// <param name="to">The values written.</param>
    /// <param name="start">The line's first position.</param>
    /// <param name="step">The distance between neighbours on the line (1 along a row, the width down a column).</param>
    /// <param name="length">How many pixels the line has.</param>
    /// <param name="reach">How far each pixel looks either way.</param>
    private static void Spread(byte[] from, byte[] to, int start, int step, int length, int reach)
    {
        // The last white pixel seen so far, and the next one ahead, decide each pixel in one walk.
        var lastWhite = int.MinValue / 2;
        var nextWhite = -1;

        for (var i = 0; i < length; i++)
        {
            if (from[start + i * step] >= Constants.Imaging.CoverageThreshold)
            {
                lastWhite = i;
            }

            if (nextWhite < i)
            {
                nextWhite = i;

                while (nextWhite < length && from[start + nextWhite * step] < Constants.Imaging.CoverageThreshold)
                {
                    nextWhite++;
                }
            }

            var near = i - lastWhite <= reach || (nextWhite < length && nextWhite - i <= reach);

            to[start + i * step] = near ? Constants.Imaging.FullCoverage : (byte)0;
        }
    }
}
