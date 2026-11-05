using DesignApp.Application.Analysis;
using DesignApp.Application.Common;

namespace DesignApp.Infrastructure.Photos;

/// <summary>
/// The pixel work of a brush stroke on a label map: a round brush stamped along the stroke's path.
/// Painting gives every covered pixel to the object; erasing takes covered pixels away from it
/// (other objects are never touched). The editor draws the same stroke with the same rules
/// (<c>features/editor/brush.ts</c>) for its live preview, so what the user sees is what is saved.
/// </summary>
internal static class OutlineBrush
{
    /// <summary>Applies a stroke to a label map.</summary>
    /// <param name="labels">One object index per pixel (0 = none); changed in place on success.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="stroke">The stroke.</param>
    /// <returns>How many pixels changed, or a failure the user can read.</returns>
    public static Result<int> Apply(byte[] labels, int width, int height, OutlineStroke stroke)
    {
        var target = (byte)stroke.ObjectIndex;
        var before = stroke.Mode is OutlineBrushMode.Erase ? labels.Count(l => l == target) : 0;
        var changed = 0;
        var r = stroke.Radius;
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
                    if (dx * dx + dy * dy > r * r)
                        continue;
                    var p = y * width + x;
                    if (stroke.Mode is OutlineBrushMode.Paint && labels[p] != target)
                    {
                        labels[p] = target;
                        changed++;
                    }
                    else if (stroke.Mode is OutlineBrushMode.Erase && labels[p] == target)
                    {
                        labels[p] = 0;
                        changed++;
                    }
                }
            }
        }

        var copy = stroke.Mode is OutlineBrushMode.Erase ? (byte[])labels.Clone() : null;
        var points = stroke.Points;
        Stamp(points[0].X * width, points[0].Y * height);
        for (var i = 1; i < points.Count; i++)
        {
            double ax = points[i - 1].X * width, ay = points[i - 1].Y * height;
            double bx = points[i].X * width, by = points[i].Y * height;
            var steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay)) / spacing));
            for (var s = 1; s <= steps; s++)
                Stamp(ax + (bx - ax) * s / steps, ay + (by - ay) * s / steps);
        }

        if (changed == 0)
            return Result.Failure<int>(stroke.Mode is OutlineBrushMode.Paint
                ? $"That part is already the {stroke.ObjectLabel}."
                : $"There's no {stroke.ObjectLabel} there to erase.");
        if (copy is not null && changed == before)
        {
            copy.CopyTo(labels, 0);
            return Result.Failure<int>($"That would erase the whole {stroke.ObjectLabel}. Use Reset to remove its color instead.");
        }
        return changed;
    }
}
