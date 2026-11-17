using DesignApp.Application.Objects;

using static DesignApp.Application.Constants;

namespace DesignApp.Infrastructure.Photos;

/// <summary>
/// The pixel work of marking a new object by hand: the strokes build a mask in order (marking adds covered pixels,
/// unmarking takes them away), then every marked pixel gets the new object's index, on top of whatever object had
/// it. The editor builds the same mask with the same rules (<c>features/editor/brush.ts</c>) for its preview.
/// </summary>
internal static class ObjectMarker
{
    /// <summary>Applies a mark to a label map.</summary>
    /// <param name="labels">One object index per pixel (0 = none); changed in place on success.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="mark">The strokes and the new object's index.</param>
    /// <returns>How many pixels the new object has, or a failure the user can read (nothing marked).</returns>
    public static Result<int> Apply(byte[] labels, int width, int height, NewObjectMark mark)
    {
        var mask = new bool[labels.Length];
        var target = (byte)mark.Index;
        var marked = 0;

        foreach (var stroke in mark.Strokes)
        {
            var value = stroke.Mode is OutlineBrushMode.Paint;

            StrokeCover.Walk(width, height, stroke.Shape, stroke.Radius, stroke.Points, p => mask[p] = value);
        }

        for (var p = 0; p < labels.Length; p++)
        {
            // A part only takes its object's pixels.
            if (!mask[p] || (mark.ParentIndex is int parent && labels[p] != parent))
            {
                continue;
            }

            labels[p] = target;
            marked++;
        }

        if (marked == 0)
        {
            return Result.Failure<int>(mark.ParentIndex is null ? Constants.Messages.EmptyMark : Constants.Messages.PartOutsideParent(mark.ParentLabel ?? ""));
        }

        return marked;
    }
}
