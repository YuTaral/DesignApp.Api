using DesignApp.Application.Objects;

using static DesignApp.Application.Constants;

namespace DesignApp.Infrastructure.Photos;

/// <summary>
/// The pixels a stroke covers, by its shape: the round brush along the path (<see cref="BrushStamp"/>), the
/// rectangle between a box's two corners, or the inside of a lasso. A pixel is inside a box or lasso when its
/// centre is. It matches the web app's <c>walkArea</c> (<c>features/editor/brush.ts</c>) exactly, so the live
/// preview is what gets saved.
/// </summary>
internal static class StrokeCover
{
    /// <summary>Calls <paramref name="onPixel"/> for every pixel the stroke covers.</summary>
    /// <param name="width">Label map width.</param>
    /// <param name="height">Label map height.</param>
    /// <param name="shape">The stroke's shape.</param>
    /// <param name="radius">The brush radius, in label map pixels (only for the brush).</param>
    /// <param name="points">The path, the box's two corners, or the lasso; each 0..1 of the photo.</param>
    /// <param name="onPixel">Called with each covered pixel's position in the map (a box or lasso calls it once per pixel).</param>
    public static void Walk(int width, int height, StrokeShape shape, double radius, IReadOnlyList<StrokePoint> points, Action<int> onPixel)
    {
        switch (shape)
        {
            case StrokeShape.Box:
                FillBox(width, height, points[0], points[1], onPixel);
                break;
            case StrokeShape.Lasso:
                FillLasso(width, height, points, onPixel);
                break;
            default:
                BrushStamp.Walk(width, height, radius, points, onPixel);
                break;
        }
    }

    /// <summary>Every pixel whose centre is inside the rectangle (edges included), clipped to the map.</summary>
    /// <param name="width">Label map width.</param>
    /// <param name="height">Label map height.</param>
    /// <param name="a">One corner.</param>
    /// <param name="b">The opposite corner.</param>
    /// <param name="onPixel">Called once per covered pixel.</param>
    private static void FillBox(int width, int height, StrokePoint a, StrokePoint b, Action<int> onPixel)
    {
        double ax = a.X * width, ay = a.Y * height, bx = b.X * width, by = b.Y * height;
        var x0 = Math.Max(0, (int)Math.Ceiling(Math.Min(ax, bx) - 0.5));
        var x1 = Math.Min(width - 1, (int)Math.Floor(Math.Max(ax, bx) - 0.5));
        var y0 = Math.Max(0, (int)Math.Ceiling(Math.Min(ay, by) - 0.5));
        var y1 = Math.Min(height - 1, (int)Math.Floor(Math.Max(ay, by) - 0.5));
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
                onPixel(y * width + x);
        }
    }

    /// <summary>
    /// Every pixel whose centre is inside the closed path, by the even-odd rule: on each row, the path's crossings
    /// with the row's centre line are sorted and the pixels between each pair are inside.
    /// </summary>
    /// <param name="width">Label map width.</param>
    /// <param name="height">Label map height.</param>
    /// <param name="points">The path, at least three points; the last joins the first.</param>
    /// <param name="onPixel">Called once per covered pixel.</param>
    private static void FillLasso(int width, int height, IReadOnlyList<StrokePoint> points, Action<int> onPixel)
    {
        var xs = new double[points.Count];
        var ys = new double[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            xs[i] = points[i].X * width;
            ys[i] = points[i].Y * height;
        }

        var crossings = new List<double>();
        for (var y = 0; y < height; y++)
        {
            var cy = y + 0.5;
            crossings.Clear();
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                if (ys[i] <= cy == ys[j] <= cy)
                    continue;
                crossings.Add(xs[j] + (cy - ys[j]) * (xs[i] - xs[j]) / (ys[i] - ys[j]));
            }
            crossings.Sort();
            for (var k = 0; k + 1 < crossings.Count; k += 2)
            {
                var x0 = Math.Max(0, (int)Math.Ceiling(crossings[k] - 0.5));
                var x1 = Math.Min(width - 1, (int)Math.Ceiling(crossings[k + 1] - 0.5) - 1);
                for (var x = x0; x <= x1; x++)
                    onPixel(y * width + x);
            }
        }
    }
}
