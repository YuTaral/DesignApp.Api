using DesignApp.Application.Objects;

namespace DesignApp.Infrastructure.Photos;

/// <summary>
/// Where a round brush lands along a path: the pixel walk shared by the outline brush and object marking. It
/// matches the web app's <c>stampStroke</c> exactly, so the live preview is what gets saved.
/// </summary>
internal static class BrushStamp
{
    /// <summary>Stamps the brush at the first point and then every half radius along the path.</summary>
    /// <param name="width">Label map width.</param>
    /// <param name="height">Label map height.</param>
    /// <param name="radius">The brush radius, in label map pixels.</param>
    /// <param name="points">The path, at least one point, each 0..1 of the photo.</param>
    /// <param name="onPixel">Called for every covered pixel (by its position in the map), once per stamp.</param>
    public static void Walk(int width, int height, double radius, IReadOnlyList<StrokePoint> points, Action<int> onPixel)
    {
        var r = radius;
        var spacing = Math.Max(0.5, r * 0.5);

        void Stamp(double cx, double cy)
        {
            var (x0, x1) = (Math.Max(0, (int)Math.Floor(cx - r)), Math.Min(width - 1, (int)Math.Ceiling(cx + r)));
            var (y0, y1) = (Math.Max(0, (int)Math.Floor(cy - r)), Math.Min(height - 1, (int)Math.Ceiling(cy + r)));
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    double dx = x + 0.5 - cx, dy = y + 0.5 - cy;
                    if (dx * dx + dy * dy <= r * r)
                        onPixel(y * width + x);
                }
            }
        }

        Stamp(points[0].X * width, points[0].Y * height);
        for (var i = 1; i < points.Count; i++)
        {
            double ax = points[i - 1].X * width, ay = points[i - 1].Y * height;
            double bx = points[i].X * width, by = points[i].Y * height;
            var steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay)) / spacing));
            for (var s = 1; s <= steps; s++)
                Stamp(ax + (bx - ax) * s / steps, ay + (by - ay) * s / steps);
        }
    }
}
