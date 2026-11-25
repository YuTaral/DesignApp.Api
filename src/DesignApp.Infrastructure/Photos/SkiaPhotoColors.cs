using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using CC = DesignApp.Infrastructure.Constants.ColorCorrection;

namespace DesignApp.Infrastructure.Photos;

/// <summary>
/// SkiaSharp implementation of <see cref="IPhotoColors"/>. The 3D view and its masks are scaled to the photo (the masks
/// without blending, so each pixel keeps one region); each object is a region, and each surface is split into one
/// region per planned color by which of them its 3D view's pixel is nearest in hue and colorfulness. Every region is
/// measured first (<see cref="RegionShift.Measure"/>); when the photo keeps its light, the light is how far the photo's
/// surfaces are from the 3D view's on average, and every region is moved to its 3D view's color in that light. The
/// regions' shifts are added up, and where regions blend at an edge they are averaged.
/// </summary>
/// <param name="logger">Logs a picture that can't be read.</param>
internal sealed partial class SkiaPhotoColors(ILogger<SkiaPhotoColors> logger) : IPhotoColors
{
    /// <inheritdoc/>
    public byte[] Correct(byte[] photo, byte[] guidePng, byte[] masksPng, PhotoRegions regions)
    {
        ArgumentNullException.ThrowIfNull(regions);

        var shifted = false;
        using var picture = Decode(photo);

        if (picture is null)
        {
            LogUnreadable("generated photo");

            return photo;
        }

        var (width, height) = (picture.Width, picture.Height);
        using var guide = Decode(guidePng, width, height, SKFilterMode.Linear);
        using var masks = Decode(masksPng, width, height, SKFilterMode.Nearest);

        if (guide is null || masks is null)
        {
            LogUnreadable(guide is null ? "3D view" : "object masks");

            return photo;
        }

        var pixels = new PhotoPixels(width, height, [], Lab(picture), Lab(guide));
        var (labels, count) = Label(masks.GetPixelSpan(), pixels.Guide, regions);
        var labelled = pixels with { Regions = labels };
        var shift = new float[width * height * 3];
        var weight = new float[width * height];
        var measured = Enumerable.Range(1, count).Select(region => RegionShift.Measure(region, labelled)).OfType<RegionColors>().ToList();
        // The surfaces' regions come after the objects' (see Label).
        var light = regions.KeepsLight ? Light(measured.Where(m => m.Region > regions.Objects.Count)) : [0f, 0f, 0f];

        foreach (var colors in measured)
        {
            shifted |= RegionShift.Apply(colors, light, labelled, shift, weight);
        }

        return shifted ? Encode(picture, labelled.Photo, shift, weight) : photo;
    }

    /// <summary>
    /// The photo's light: how far its surfaces' typical colors are from the 3D view's, on average by their size (the
    /// warm glow of golden hour, the lamplight of an evening).
    /// </summary>
    /// <param name="surfaces">The surfaces' regions, measured.</param>
    /// <returns>Three Lab values; zeros when no surface could be measured.</returns>
    private static float[] Light(IEnumerable<RegionColors> surfaces)
    {
        var (sum, size) = (new double[3], 0.0);

        foreach (var surface in surfaces)
        {
            for (var c = 0; c < 3; c++)
            {
                sum[c] += (surface.Photo[c] - surface.Guide[c]) * surface.Size;
            }

            size += surface.Size;
        }

        return size == 0 ? [0f, 0f, 0f] : [.. sum.Select(s => (float)(s / size))];
    }

    /// <summary>Logs that a picture can't be read, so the photo's colors are left as they are.</summary>
    /// <param name="picture">Which picture.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The {Picture} can't be read; the photo's colors are left as they are")]
    private partial void LogUnreadable(string picture);

    /// <summary>Reads a picture as RGBA pixels, scaled to a size when it has another.</summary>
    /// <param name="bytes">The picture (JPEG or PNG).</param>
    /// <param name="width">The size's width; <c>null</c> to keep the picture's.</param>
    /// <param name="height">The size's height.</param>
    /// <param name="filter">How to scale it: <see cref="SKFilterMode.Nearest"/> keeps each pixel's value.</param>
    /// <returns>The pixels, or <c>null</c> when the picture can't be read.</returns>
    private static SKBitmap? Decode(byte[] bytes, int? width = null, int? height = null, SKFilterMode filter = SKFilterMode.Linear)
    {
        // SKBitmap.Decode throws on bytes no codec knows; a codec is null for them instead.
        using var stream = new SKMemoryStream(bytes);
        using var codec = SKCodec.Create(stream);

        if (codec is null)
        {
            return null;
        }

        using var decoded = SKBitmap.Decode(codec);

        if (decoded is null)
        {
            return null;
        }

        var info = new SKImageInfo(width ?? decoded.Width, height ?? decoded.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);

        return decoded.Width == info.Width && decoded.Height == info.Height
            ? decoded.Copy(SKColorType.Rgba8888)
            : decoded.Resize(info, new SKSamplingOptions(filter));
    }

    /// <summary>A picture's pixels in Lab.</summary>
    /// <param name="bitmap">The picture (RGBA).</param>
    /// <returns>Three Lab values per pixel, row by row.</returns>
    private static float[] Lab(SKBitmap bitmap)
    {
        var rgba = bitmap.GetPixelSpan();
        var lab = new float[bitmap.Width * bitmap.Height * 3];

        for (var i = 0; i < bitmap.Width * bitmap.Height; i++)
        {
            (lab[i * 3], lab[i * 3 + 1], lab[i * 3 + 2]) = LabColor.FromRgb(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2]);
        }

        return lab;
    }

    /// <summary>
    /// Each pixel's region: an object's own, or, on a surface, the one of its planned color nearest to the 3D view's
    /// pixel (lightness counting little: the 3D view's shading darkens a surface).
    /// </summary>
    /// <param name="masks">The masks' pixels (RGBA; the id in red).</param>
    /// <param name="guide">The 3D view's pixels in Lab.</param>
    /// <param name="regions">The objects and surfaces to correct.</param>
    /// <returns>Each pixel's region (0 for none, else from 1), and how many regions there are.</returns>
    private static (int[] Labels, int Count) Label(ReadOnlySpan<byte> masks, float[] guide, PhotoRegions regions)
    {
        var labels = new int[masks.Length / 4];
        var objects = new Dictionary<int, int>();
        var surfaces = new Dictionary<int, (int First, (float L, float A, float B)[] Colors)>();
        var next = 1;

        foreach (var id in regions.Objects.Order())
        {
            objects[id] = next++;
        }

        foreach (var (id, colors) in regions.Surfaces)
        {
            surfaces[id] = (next, [.. colors.Select(LabColor.FromHex)]);
            next += colors.Count;
        }

        for (var i = 0; i < labels.Length; i++)
        {
            var id = masks[i * 4];

            if (objects.TryGetValue(id, out var region))
            {
                labels[i] = region;
            }
            else if (surfaces.TryGetValue(id, out var surface))
            {
                labels[i] = surface.First + Nearest(surface.Colors, guide[i * 3], guide[i * 3 + 1], guide[i * 3 + 2]);
            }
        }

        return (labels, next - 1);
    }

    /// <summary>Which planned color a 3D view's pixel is nearest, by hue and colorfulness more than lightness.</summary>
    /// <param name="colors">The planned colors in Lab.</param>
    /// <param name="l">The pixel's lightness.</param>
    /// <param name="a">Its green-red axis.</param>
    /// <param name="b">Its blue-yellow axis.</param>
    /// <returns>The nearest color's position.</returns>
    private static int Nearest((float L, float A, float B)[] colors, float l, float a, float b)
    {
        var (best, nearest) = (double.MaxValue, 0);

        for (var k = 0; k < colors.Length; k++)
        {
            var dl = (l - colors[k].L) * CC.SurfaceLightnessWeight;
            var distance = dl * dl + (a - colors[k].A) * (a - colors[k].A) + (b - colors[k].B) * (b - colors[k].B);

            if (distance < best)
            {
                (best, nearest) = (distance, k);
            }
        }

        return nearest;
    }

    /// <summary>The photo with each pixel's averaged shift applied, as a JPEG.</summary>
    /// <param name="picture">The photo (RGBA), changed in place.</param>
    /// <param name="lab">Its pixels in Lab.</param>
    /// <param name="shift">Each pixel's summed shift.</param>
    /// <param name="weight">Each pixel's summed weight: a sum above 1 (where regions blend) averages their shifts.</param>
    private static byte[] Encode(SKBitmap picture, float[] lab, float[] shift, float[] weight)
    {
        var rgba = picture.GetPixelSpan();

        for (var i = 0; i < weight.Length; i++)
        {
            if (weight[i] < CC.MinWeight)
            {
                continue;
            }

            var share = 1 / Math.Max(weight[i], 1f);

            (rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2]) = LabColor.ToRgb(
                lab[i * 3] + shift[i * 3] * share, lab[i * 3 + 1] + shift[i * 3 + 1] * share, lab[i * 3 + 2] + shift[i * 3 + 2] * share);
        }

        using var image = SKImage.FromBitmap(picture);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, Application.Constants.Photos.JpegQuality);

        return data.ToArray();
    }
}
