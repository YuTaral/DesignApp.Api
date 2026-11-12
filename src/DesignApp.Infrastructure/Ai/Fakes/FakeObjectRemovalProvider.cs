using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace DesignApp.Infrastructure.Ai.Fakes;

/// <summary>
/// The free eraser for development and tests: fills the masked area from its edge inward, each pixel the average of
/// the filled pixels next to it. It looks smeared, not real, but it changes the photo the way the real one does.
/// </summary>
/// <param name="options">The fake step delay.</param>
internal sealed class FakeObjectRemovalProvider(IOptions<AiOptions> options) : IObjectRemovalProvider
{
    /// <inheritdoc/>
    public async Task<Result<byte[]>> RemoveAsync(byte[] photoJpeg, byte[] maskPng, bool brushed, CancellationToken cancellationToken)
    {
        await Task.Delay(options.Value.FakeStepDelay, cancellationToken);
        return Fill(photoJpeg, maskPng);
    }

    /// <summary>Fills the masked pixels from the outside in.</summary>
    /// <param name="photoJpeg">The photo.</param>
    /// <param name="maskPng">The mask, white = fill.</param>
    /// <returns>The filled photo as a JPEG.</returns>
    internal static byte[] Fill(byte[] photoJpeg, byte[] maskPng)
    {
        using var photo = SKBitmap.Decode(photoJpeg)?.Copy(SKColorType.Rgba8888) ?? throw new InvalidOperationException("The photo can't be decoded.");
        var (width, height) = (photo.Width, photo.Height);
        using var maskCodec = SKCodec.Create(new MemoryStream(maskPng));
        using var mask = SKBitmap.Decode(maskCodec, new SKImageInfo(width, height, SKColorType.Gray8, SKAlphaType.Opaque))
            ?? throw new InvalidOperationException("The mask can't be decoded.");
        var pixels = photo.GetPixelSpan();
        var holes = mask.GetPixelSpan();
        var known = new bool[width * height];
        for (var p = 0; p < known.Length; p++)
            known[p] = holes[p] < Constants.Imaging.CoverageThreshold;

        // Start at the hole pixels that touch a known pixel, then work inward one ring at a time.
        var queue = new Queue<int>();
        var queued = new bool[known.Length];
        for (var p = 0; p < known.Length; p++)
        {
            if (known[p] || !Neighbours(p, width, height).Any(q => known[q]))
                continue;
            queued[p] = true;
            queue.Enqueue(p);
        }
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            int r = 0, g = 0, b = 0, n = 0;
            foreach (var q in Neighbours(p, width, height))
            {
                if (known[q])
                {
                    (r, g, b, n) = (r + pixels[q * 4], g + pixels[q * 4 + 1], b + pixels[q * 4 + 2], n + 1);
                }
                else if (!queued[q])
                {
                    queued[q] = true;
                    queue.Enqueue(q);
                }
            }
            if (n > 0)
                (pixels[p * 4], pixels[p * 4 + 1], pixels[p * 4 + 2]) = ((byte)(r / n), (byte)(g / n), (byte)(b / n));
            known[p] = true;
        }

        using var image = SKImage.FromPixels(photo.PeekPixels());
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, Application.Constants.Photos.JpegQuality);
        return encoded.ToArray();
    }

    /// <summary>The up to 8 pixels around one.</summary>
    /// <param name="p">The pixel.</param>
    /// <param name="width">Image width.</param>
    /// <param name="height">Image height.</param>
    private static IEnumerable<int> Neighbours(int p, int width, int height)
    {
        var (x, y) = (p % width, p / width);
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if ((dx != 0 || dy != 0) && x + dx >= 0 && x + dx < width && y + dy >= 0 && y + dy < height)
                    yield return (y + dy) * width + x + dx;
            }
        }
    }
}
