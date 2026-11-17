using DesignApp.Application.Objects;
using SkiaSharp;

namespace DesignApp.Infrastructure.Photos;

/// <summary>Pixel work on masks and label maps: where a mask's covered pixels are, and gray pixels as a PNG.</summary>
internal static class MaskPixels
{
    /// <summary>
    /// The smallest rectangle, in the mask's own pixels, holding every pixel it covers (at least
    /// <see cref="Constants.Imaging.CoverageThreshold"/>); <c>null</c> when it covers none.
    /// </summary>
    /// <param name="mask">The mask.</param>
    public static PixelRect? CoveredRect(ObjectMask mask)
    {
        var (x0, y0, x1, y1) = (mask.Width, mask.Height, -1, -1);

        for (var y = 0; y < mask.Height; y++)
        {
            for (var x = 0; x < mask.Width; x++)
            {
                if (mask.Coverage[y * mask.Width + x] < Constants.Imaging.CoverageThreshold)
                {
                    continue;
                }

                (x0, y0, x1, y1) = (Math.Min(x0, x), Math.Min(y0, y), Math.Max(x1, x), Math.Max(y1, y));
            }
        }

        return x1 < 0 ? null : new PixelRect(x0, y0, x1 + 1, y1 + 1);
    }

    /// <summary>Encodes one byte per pixel (a label map, or a mask's coverage) as a grayscale PNG.</summary>
    /// <param name="gray">The pixels, row by row.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    public static byte[] GrayPng(byte[] gray, int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Gray8, SKAlphaType.Opaque));

        gray.CopyTo(bitmap.GetPixelSpan());
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, Constants.Imaging.PngQuality);

        return png.ToArray();
    }
}
