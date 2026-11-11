using DesignApp.Application.Objects;


namespace DesignApp.Application.Interfaces;

/// <summary>The pixel work of the analysis: scaling the photo and building the label map.</summary>
public interface IRoomImaging
{
    /// <summary>Makes the copy of a stored photo that the AI providers see.</summary>
    /// <param name="photo">The stored room photo (JPEG).</param>
    /// <returns>The photo scaled to at most <see cref="Constants.Analysis.MaxLongEdge"/>.</returns>
    AnalysisImage PrepareForAnalysis(Stream photo);

    /// <summary>
    /// Combines the outlines into one label map. Smaller objects sit on top of larger ones (a cushion
    /// on a sofa on the floor), objects left with almost no visible pixels are dropped, the masks are
    /// cleaned up (speckles removed, enclosed holes filled, walls, floors and ceilings grown into gaps
    /// up to the photo's edges), and each kept object's box and main color are measured from its pixels.
    /// </summary>
    /// <param name="image">The analysis image the outlines belong to.</param>
    /// <param name="objects">The outlined objects.</param>
    LabelMap BuildLabelMap(AnalysisImage image, IReadOnlyList<SegmentedObject> objects);

    /// <summary>Paints or erases one object's outline with a brush stroke the user drew.</summary>
    /// <param name="labelMapPng">The current label map.</param>
    /// <param name="stroke">The stroke.</param>
    /// <returns>The changed label map, or a failure the user can read (such as a stroke that changes nothing).</returns>
    Result<byte[]> PaintOutline(byte[] labelMapPng, OutlineStroke stroke);

    /// <summary>
    /// Turns the pixels the user marked into a new object, on top of whatever was there, and measures its box and
    /// main color from the photo.
    /// </summary>
    /// <param name="labelMapPng">The label map the user marked on.</param>
    /// <param name="photo">The stored room photo (JPEG).</param>
    /// <param name="mark">The strokes and the new object's index.</param>
    /// <returns>The new label map with the object's box and color, or a failure the user can read (nothing marked).</returns>
    Result<MarkedObject> MarkObject(byte[] labelMapPng, Stream photo, NewObjectMark mark);
}
