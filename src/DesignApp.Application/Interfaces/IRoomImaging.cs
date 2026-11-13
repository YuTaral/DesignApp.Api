using DesignApp.Application.Objects;
using DesignApp.Domain.Objects;


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

    /// <summary>
    /// Makes outlined parts of an object into parts in the label map: each takes the object's pixels its mask covers
    /// (a smaller part on top of a larger one), and parts left with almost no pixels are dropped.
    /// </summary>
    /// <param name="labelMapPng">The label map the parts are added to.</param>
    /// <param name="photo">The stored room photo (JPEG), for each part's box and main color.</param>
    /// <param name="whole">The object.</param>
    /// <param name="firstIndex">The index the first part gets; the others follow.</param>
    /// <param name="parts">The parts with their masks, at the analysis size.</param>
    /// <returns>The new label map and the parts in it, or a failure when none of them covers the object.</returns>
    Result<LabelMap> AddParts(byte[] labelMapPng, Stream photo, DetectedObject whole, int firstIndex, IReadOnlyList<SegmentedObject> parts);

    /// <summary>Gives a part's pixels back to the object it is a part of.</summary>
    /// <param name="labelMapPng">The label map the merge starts from.</param>
    /// <param name="partIndex">The part.</param>
    /// <param name="parentIndex">The object it is a part of.</param>
    /// <param name="partLabel">The part's name, for messages.</param>
    /// <returns>The new label map, or a failure when the part has no pixels there.</returns>
    Result<byte[]> MergePart(byte[] labelMapPng, int partIndex, int parentIndex, string partLabel);

    /// <summary>The mask of the objects to remove, at the photo's size: white where any of them is, black elsewhere.</summary>
    /// <param name="labelMapPng">The label map they are removed from.</param>
    /// <param name="indexes">The objects.</param>
    /// <param name="photoWidth">The stored photo's width.</param>
    /// <param name="photoHeight">The stored photo's height.</param>
    /// <returns>The mask PNG, or <c>null</c> when none of them has pixels in this label map.</returns>
    byte[]? RemovalMask(byte[] labelMapPng, IReadOnlyCollection<int> indexes, int photoWidth, int photoHeight);

    /// <summary>
    /// The files of the room after objects were removed: the new photo as a JPEG at the stored size, and the label
    /// map with their pixels given to the objects around them (matched by color in the new photo).
    /// </summary>
    /// <param name="labelMapPng">The label map they were removed from.</param>
    /// <param name="indexes">The removed objects.</param>
    /// <param name="newPhoto">The photo with the object removed, as the provider returned it.</param>
    /// <param name="photoWidth">The stored photo's width.</param>
    /// <param name="photoHeight">The stored photo's height.</param>
    RemovedObject AfterRemoval(byte[] labelMapPng, IReadOnlyCollection<int> indexes, byte[] newPhoto, int photoWidth, int photoHeight);
}
