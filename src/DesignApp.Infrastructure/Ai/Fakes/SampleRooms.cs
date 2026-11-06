using System.Text.Json;
using System.Text.Json.Serialization;
using DesignApp.Application.Analysis;
using DesignApp.Domain.Common;
using DesignApp.Domain.Rooms;
using SkiaSharp;

namespace DesignApp.Infrastructure.Ai.Fakes;

/// <summary>
/// Hand-traced analyses of the sample room photos (the <c>Samples/*.json</c> files): every object's
/// label, category and outline. The fake providers recognize an uploaded sample photo by its
/// <see cref="Fingerprint"/> and return its traced objects, so development shows the real objects
/// of that photo without calling the AI. Any other photo gets the generic layout instead.
/// </summary>
internal static class SampleRooms
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>All traced samples, read once from the embedded JSON files.</summary>
    internal static IReadOnlyList<SampleRoom> All { get; } = Load();

    /// <summary>Finds the sample an analysis image is a copy of.</summary>
    /// <param name="image">The uploaded photo, as the AI providers see it.</param>
    /// <returns>The matching sample, or <c>null</c> for any other photo.</returns>
    internal static SampleRoom? Match(AnalysisImage image)
    {
        var aspect = image.Width / (double)image.Height;
        var candidates = All.Where(s => Math.Abs(s.Width / (double)s.Height / aspect - 1) <= Constants.Samples.MaxAspectDifference).ToList();
        if (candidates.Count == 0)
            return null;

        var fingerprint = Fingerprint(image.Jpeg);
        return candidates
            .Select(s => (Sample: s, Difference: Difference(fingerprint, s.FingerprintBytes)))
            .Where(c => c.Difference <= Constants.Samples.MaxDifference)
            .OrderBy(c => c.Difference)
            .Select(c => c.Sample)
            .FirstOrDefault();
    }

    /// <summary>
    /// The mean brightness (Rec. 601 luma, over white for transparent pixels) of each cell of a
    /// 16 x 16 grid. Must match <c>fingerprint.py</c>, which wrote the samples' fingerprints.
    /// </summary>
    /// <param name="encoded">The image file.</param>
    internal static byte[] Fingerprint(byte[] encoded)
    {
        using var codec = SKCodec.Create(new MemoryStream(encoded)) ?? throw new ArgumentException("Not an image.", nameof(encoded));
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul, SKColorSpace.CreateSrgb());
        using var bitmap = SKBitmap.Decode(codec, info);
        var pixels = bitmap.GetPixelSpan();
        var (w, h) = (info.Width, info.Height);

        var cells = new byte[Constants.Samples.Grid * Constants.Samples.Grid];
        for (var cy = 0; cy < Constants.Samples.Grid; cy++)
        {
            for (var cx = 0; cx < Constants.Samples.Grid; cx++)
            {
                var (x0, x1, y0, y1) = (cx * w / Constants.Samples.Grid, (cx + 1) * w / Constants.Samples.Grid, cy * h / Constants.Samples.Grid, (cy + 1) * h / Constants.Samples.Grid);
                var total = 0.0;
                for (var y = y0; y < y1; y++)
                {
                    for (var x = x0; x < x1; x++)
                    {
                        var p = (y * w + x) * 4;
                        var a = pixels[p + 3] / 255.0;
                        double Over(byte c) => c * a + 255 * (1 - a);
                        total += 0.299 * Over(pixels[p]) + 0.587 * Over(pixels[p + 1]) + 0.114 * Over(pixels[p + 2]);
                    }
                }
                cells[cy * Constants.Samples.Grid + cx] = (byte)Math.Round(total / ((x1 - x0) * (y1 - y0)));
            }
        }
        return cells;
    }

    /// <summary>The mean absolute difference between two fingerprints.</summary>
    /// <param name="a">One fingerprint.</param>
    /// <param name="b">The other.</param>
    internal static double Difference(byte[] a, byte[] b) => a.Zip(b, (x, y) => Math.Abs(x - y)).Average();

    /// <summary>
    /// Reads the embedded sample files. A recorded SAM 3 analysis (<c>name.sam3.json</c>) takes
    /// the place of the hand-traced one (<c>name.json</c>) of the same photo.
    /// </summary>
    private static List<SampleRoom> Load()
    {
        var assembly = typeof(SampleRooms).Assembly;
        return [.. assembly.GetManifestResourceNames()
            .Where(n => n.Contains(Constants.Samples.ResourceFolder, StringComparison.Ordinal) && n.EndsWith(Constants.Samples.Extension, StringComparison.Ordinal))
            .Select(n =>
            {
                using var stream = assembly.GetManifestResourceStream(n)!;
                var sample = JsonSerializer.Deserialize<SampleRoom>(stream, Options)
                    ?? throw new InvalidDataException($"Sample {n} is empty.");
                return (Sample: sample, Recorded: n.EndsWith(Constants.Samples.RecordedSuffix, StringComparison.Ordinal));
            })
            .GroupBy(s => s.Sample.Name)
            .Select(g => g.OrderByDescending(s => s.Recorded).First().Sample)
            .OrderBy(s => s.Name, StringComparer.Ordinal)];
    }
}

/// <summary>One traced sample photo.</summary>
/// <param name="Name">The photo's file name without extension.</param>
/// <param name="Width">The photo's width; shape coordinates are in its pixels.</param>
/// <param name="Height">The photo's height.</param>
/// <param name="Fingerprint">The photo's <see cref="SampleRooms.Fingerprint"/>, as hex.</param>
/// <param name="Objects">The traced objects, most important first.</param>
internal sealed record SampleRoom(string Name, int Width, int Height, string Fingerprint, IReadOnlyList<SampleObject> Objects)
{
    /// <summary>The fingerprint as bytes.</summary>
    public byte[] FingerprintBytes { get; } = Convert.FromHexString(Fingerprint);

    /// <summary>The objects, with every recorded mask told the photo size it covers.</summary>
    public IReadOnlyList<SampleObject> Objects { get; } = WithPhotoSize(Objects, Width, Height);

    /// <summary>Tells each shape the photo's size, which recorded masks are stretched over.</summary>
    /// <param name="objects">The objects as read.</param>
    /// <param name="width">The photo's width.</param>
    /// <param name="height">The photo's height.</param>
    private static IReadOnlyList<SampleObject> WithPhotoSize(IReadOnlyList<SampleObject> objects, int width, int height)
    {
        foreach (var shape in objects.SelectMany(o => o.Shapes))
            shape.PhotoSize = new SKSize(width, height);
        return objects;
    }

    /// <summary>The objects as the inventory step reports them, with boxes from their outlines.</summary>
    public IReadOnlyList<InventoryItem> Inventory() =>
        [.. Objects.Select(o => new InventoryItem(o.Label, o.Category, o.Box(Width, Height), HexColor.Parse(Domain.Constants.Colors.Fallback)))];

    /// <summary>Draws one object's outline at the analysis image's size.</summary>
    /// <param name="label">The object's label.</param>
    /// <param name="width">The analysis image's width.</param>
    /// <param name="height">The analysis image's height.</param>
    /// <returns>The mask, or <c>null</c> when this sample has no object with that label.</returns>
    public ObjectMask? Outline(string label, int width, int height)
    {
        var sampleObject = Objects.FirstOrDefault(o => o.Label == label);
        if (sampleObject is null)
            return null;

        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Alpha8, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(width / (float)Width, height / (float)Height);
            foreach (var shape in sampleObject.Shapes)
                shape.Draw(canvas);
        }
        return new ObjectMask(width, height, bitmap.GetPixelSpan().ToArray());
    }
}

/// <summary>One traced object.</summary>
/// <param name="Label">What it is.</param>
/// <param name="Category">The kind of object.</param>
/// <param name="Shapes">Its outline: the union of these shapes.</param>
internal sealed record SampleObject(string Label, ObjectCategory Category, IReadOnlyList<SampleShape> Shapes)
{
    /// <summary>The bounding box of all shapes, normalized and clipped to the photo.</summary>
    /// <param name="width">The photo's width.</param>
    /// <param name="height">The photo's height.</param>
    public BoundingBox Box(int width, int height)
    {
        var bounds = Shapes.Select(s => s.Bounds()).Aggregate(SKRect.Union);
        bounds.Intersect(new SKRect(0, 0, width, height));
        return new BoundingBox(bounds.Left / width, bounds.Top / height, bounds.Width / width, bounds.Height / height);
    }
}

/// <summary>
/// One shape of a traced outline, in photo pixels. Exactly one of the four is set: a filled
/// polygon (x, y pairs), a filled ellipse (center x, center y, radius x, radius y), a line
/// through points (x, y pairs) drawn <see cref="Width"/> pixels thick, or a recorded mask (a
/// grayscale PNG, white = object, stretched over the whole photo).
/// </summary>
/// <param name="Polygon">Polygon corners.</param>
/// <param name="Ellipse">Ellipse center and radii.</param>
/// <param name="Line">Line points.</param>
/// <param name="Width">Line thickness.</param>
/// <param name="Mask">A recorded mask as base64 PNG (see <see cref="RecordingSegmentationProvider"/>).</param>
internal sealed record SampleShape(float[]? Polygon, float[]? Ellipse, float[]? Line, float Width, string? Mask)
{
    /// <summary>The decoded mask, or <c>null</c> for a drawn shape.</summary>
    private SKImage? MaskImage => Mask is null ? null : _maskImage ??= SKImage.FromEncodedData(Convert.FromBase64String(Mask));
    private SKImage? _maskImage;

    /// <summary>The photo size a mask is stretched over; set by <see cref="SampleRoom"/>.</summary>
    internal SKSize PhotoSize { get; set; }

    /// <summary>The shape as a Skia path.</summary>
    private SKPath Path()
    {
        using var builder = new SKPathBuilder();
        if (Polygon is { } polygon)
            builder.AddPoly(Points(polygon), close: true);
        else if (Ellipse is [var cx, var cy, var rx, var ry])
            builder.AddOval(new SKRect(cx - rx, cy - ry, cx + rx, cy + ry));
        else if (Line is { } line)
            builder.AddPoly(Points(line), close: false);
        return builder.Detach();
    }

    /// <summary>Fills (or, for a line, strokes) the shape in opaque white.</summary>
    /// <param name="canvas">The mask canvas, already scaled to photo pixels.</param>
    public void Draw(SKCanvas canvas)
    {
        if (MaskImage is { } mask)
        {
            // Mask brightness becomes coverage on the alpha-only canvas.
            using var maskPaint = new SKPaint { ColorFilter = SKColorFilter.CreateLumaColor() };
            canvas.DrawImage(mask, SKRect.Create(PhotoSize), new SKSamplingOptions(SKFilterMode.Nearest), maskPaint);
            return;
        }
        using var path = Path();
        using var paint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = false,
            Style = Line is null ? SKPaintStyle.Fill : SKPaintStyle.Stroke,
            StrokeWidth = Width,
            StrokeCap = SKStrokeCap.Butt,
            StrokeJoin = SKStrokeJoin.Round,
        };
        canvas.DrawPath(path, paint);
    }

    /// <summary>The area the shape covers, including a line's thickness.</summary>
    public SKRect Bounds()
    {
        if (MaskImage is { } mask)
            return MaskBounds(mask);
        using var path = Path();
        var bounds = path.Bounds;
        if (Line is not null)
            bounds.Inflate(Width / 2, Width / 2);
        return bounds;
    }

    /// <summary>The area a mask covers, in photo pixels.</summary>
    /// <param name="mask">The decoded mask.</param>
    private SKRect MaskBounds(SKImage mask)
    {
        using var bitmap = SKBitmap.FromImage(mask);
        var (minX, minY, maxX, maxY) = (mask.Width, mask.Height, -1, -1);
        for (var y = 0; y < mask.Height; y++)
        {
            for (var x = 0; x < mask.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Red < Constants.Imaging.CoverageThreshold)
                    continue;
                (minX, minY, maxX, maxY) = (Math.Min(minX, x), Math.Min(minY, y), Math.Max(maxX, x), Math.Max(maxY, y));
            }
        }
        if (maxX < 0)
            return SKRect.Empty;
        var (sx, sy) = (PhotoSize.Width / mask.Width, PhotoSize.Height / mask.Height);
        return new SKRect(minX * sx, minY * sy, (maxX + 1) * sx, (maxY + 1) * sy);
    }

    /// <summary>Turns x, y pairs into points.</summary>
    /// <param name="coordinates">x0, y0, x1, y1, ...</param>
    private static SKPoint[] Points(float[] coordinates) =>
        [.. Enumerable.Range(0, coordinates.Length / 2).Select(i => new SKPoint(coordinates[i * 2], coordinates[i * 2 + 1]))];
}
