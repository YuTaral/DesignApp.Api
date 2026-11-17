using DesignApp.Application.Objects;
using DesignApp.Infrastructure.Photos;
using SkiaSharp;

namespace DesignApp.Infrastructure.Ai.Fakes;

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
        {
            builder.AddPoly(Points(polygon), close: true);
        }
        else if (Ellipse is [var cx, var cy, var rx, var ry])
        {
            builder.AddOval(new SKRect(cx - rx, cy - ry, cx + rx, cy + ry));
        }
        else if (Line is { } line)
        {
            builder.AddPoly(Points(line), close: false);
        }

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
        {
            return MaskBounds(mask);
        }

        using var path = Path();
        var bounds = path.Bounds;

        if (Line is not null)
        {
            bounds.Inflate(Width / 2, Width / 2);
        }

        return bounds;
    }

    /// <summary>The area a mask covers, in photo pixels.</summary>
    /// <param name="mask">The decoded mask.</param>
    private SKRect MaskBounds(SKImage mask)
    {
        // The recorded masks are black and white, so their gray is the coverage.
        using var gray = new SKBitmap(new SKImageInfo(mask.Width, mask.Height, SKColorType.Gray8, SKAlphaType.Opaque));
        var (sx, sy) = (PhotoSize.Width / mask.Width, PhotoSize.Height / mask.Height);

        mask.ReadPixels(gray.Info, gray.GetPixels(), gray.RowBytes, 0, 0);

        return MaskPixels.CoveredRect(new ObjectMask(mask.Width, mask.Height, gray.GetPixelSpan().ToArray())) is { } r
            ? new SKRect(r.X0 * sx, r.Y0 * sy, r.X1 * sx, r.Y1 * sy)
            : SKRect.Empty;
    }

    /// <summary>Turns x, y pairs into points.</summary>
    /// <param name="coordinates">x0, y0, x1, y1, ...</param>
    private static SKPoint[] Points(float[] coordinates)
    {
        return [.. Enumerable.Range(0, coordinates.Length / 2).Select(i => new SKPoint(coordinates[i * 2], coordinates[i * 2 + 1]))];
    }
}
