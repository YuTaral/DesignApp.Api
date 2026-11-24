using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using SkiaSharp;

namespace DesignApp.Infrastructure.Photos;

/// <summary>
/// SkiaSharp implementation of <see cref="IPanoramaImaging"/>. A view is cut the way a camera at the panorama's spot
/// would see it: each of its pixels is a ray from the camera, turned by the view's tilt and then its turn, and takes the
/// panorama's color where that ray points (blended from the four nearest pixels). The panorama's middle is straight
/// ahead; its left and right ends meet behind.
/// </summary>
internal sealed class SkiaPanoramaImaging : IPanoramaImaging
{
    /// <inheritdoc/>
    public Result<PanoramaPicture> Prepare(byte[] image, int width, int height)
    {
        using var decoded = SKBitmap.Decode(image);

        if (decoded is null || decoded.Width < 2 || decoded.Height < 1)
        {
            return Result.Failure<PanoramaPicture>(Application.Constants.Messages.PanoramaPicture);
        }

        using var sized = decoded.Width == width && decoded.Height == height
            ? decoded.Copy()
            : decoded.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell));

        return new PanoramaPicture(Encode(sized), width, height);
    }

    /// <inheritdoc/>
    public byte[] Cut(byte[] panorama, PanoramaView view, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(view);

        using var decoded = SKBitmap.Decode(panorama) ?? throw new InvalidDataException("The 360-degree photo can't be read.");
        using var source = decoded.ColorType == SKColorType.Rgba8888 ? decoded.Copy() : decoded.Copy(SKColorType.Rgba8888);
        using var result = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var (sourceWidth, sourceHeight) = (source.Width, source.Height);
        var from = source.GetPixelSpan();
        var to = result.GetPixelSpan();
        var focal = height / 2.0 / Math.Tan(view.Fov * Math.PI / 360);
        var (pitch, yaw) = (view.Pitch * Math.PI / 180, view.Yaw * Math.PI / 180);
        var (cosPitch, sinPitch, cosYaw, sinYaw) = (Math.Cos(pitch), Math.Sin(pitch), Math.Cos(yaw), Math.Sin(yaw));

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // The ray through this pixel, right, up and forward, tilted, then turned.
                var (dx, dy, dz) = (x - width / 2.0 + 0.5, -(y - height / 2.0 + 0.5), focal);
                var length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                var (rx, ry, rz) = (dx / length, dy / length, dz / length);
                var (up, ahead) = (ry * cosPitch + rz * sinPitch, -ry * sinPitch + rz * cosPitch);
                var (across, forward) = (rx * cosYaw + ahead * sinYaw, -rx * sinYaw + ahead * cosYaw);
                var u = (Math.Atan2(across, forward) + Math.PI) / (2 * Math.PI) * sourceWidth - 0.5;
                var v = (Math.PI / 2 - Math.Asin(Math.Clamp(up, -1, 1))) / Math.PI * sourceHeight - 0.5;

                Blend(from, sourceWidth, sourceHeight, u, v, to.Slice((y * width + x) * 4, 4));
            }
        }

        return Encode(result);
    }

    /// <summary>The panorama's color at a point between pixels: the four around it, weighted by nearness; across its ends.</summary>
    /// <param name="from">The panorama's pixels, four bytes each.</param>
    /// <param name="width">Its width.</param>
    /// <param name="height">Its height.</param>
    /// <param name="u">Across, in pixels.</param>
    /// <param name="v">Down, in pixels.</param>
    /// <param name="to">The four bytes to fill.</param>
    private static void Blend(ReadOnlySpan<byte> from, int width, int height, double u, double v, Span<byte> to)
    {
        var (left, top) = ((int)Math.Floor(u), Math.Clamp((int)Math.Floor(v), 0, height - 2));
        var (across, down) = (u - left, Math.Clamp(v - top, 0, 1));
        var (x0, x1) = (Mod(left, width), Mod(left + 1, width));

        for (var c = 0; c < 4; c++)
        {
            var upper = from[(top * width + x0) * 4 + c] * (1 - across) + from[(top * width + x1) * 4 + c] * across;
            var lower = from[((top + 1) * width + x0) * 4 + c] * (1 - across) + from[((top + 1) * width + x1) * 4 + c] * across;

            to[c] = (byte)Math.Clamp(Math.Round(upper * (1 - down) + lower * down), 0, 255);
        }
    }

    /// <summary>A remainder that is never negative, to go round the panorama's ends.</summary>
    /// <param name="value">The value.</param>
    /// <param name="size">The size to go round.</param>
    private static int Mod(int value, int size)
    {
        return ((value % size) + size) % size;
    }

    /// <summary>A bitmap as a JPEG at the stored photos' quality.</summary>
    /// <param name="bitmap">The bitmap.</param>
    private static byte[] Encode(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, Application.Constants.Photos.JpegQuality);

        return data.ToArray();
    }
}
