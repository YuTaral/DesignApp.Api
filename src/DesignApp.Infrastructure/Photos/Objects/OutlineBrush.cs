using DesignApp.Application.Objects;

using static DesignApp.Application.Constants;

namespace DesignApp.Infrastructure.Photos;

/// <summary>
/// The pixel work of a stroke on a label map: a round brush stamped along the stroke's path, or the area of a box or
/// lasso (<see cref="StrokeCover"/>).
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
        // A part only takes its object's pixels, and erasing gives them back to the object.
        var parent = stroke.ParentIndex is int index ? (byte)index : (byte?)null;
        var before = stroke.Mode is OutlineBrushMode.Erase ? labels.Count(l => l == target) : 0;
        var changed = 0;
        var copy = stroke.Mode is OutlineBrushMode.Erase ? (byte[])labels.Clone() : null;
        StrokeCover.Walk(width, height, stroke.Shape, stroke.Radius, stroke.Points, p =>
        {
            if (stroke.Mode is OutlineBrushMode.Paint && labels[p] != target && (parent is null || labels[p] == parent))
            {
                labels[p] = target;
                changed++;
            }
            else if (stroke.Mode is OutlineBrushMode.Erase && labels[p] == target)
            {
                labels[p] = parent ?? 0;
                changed++;
            }
        });

        if (changed == 0)
            return Result.Failure<int>(stroke.Mode is OutlineBrushMode.Paint
                ? Constants.Messages.AlreadyThere(stroke.ObjectLabel)
                : Constants.Messages.NothingToErase(stroke.ObjectLabel));
        if (copy is not null && changed == before)
        {
            copy.CopyTo(labels, 0);
            return Result.Failure<int>(Constants.Messages.WouldEraseAll(stroke.ObjectLabel));
        }
        return changed;
    }
}
