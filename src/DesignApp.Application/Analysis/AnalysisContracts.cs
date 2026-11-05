using DesignApp.Application.Common;
using DesignApp.Domain.Common;
using DesignApp.Domain.Rooms;

namespace DesignApp.Application.Analysis;

/// <summary>Limits for the AI analysis of a room photo.</summary>
public static class AnalysisRules
{
    /// <summary>
    /// The AI works on a copy of the photo scaled to this long edge. More pixels cost more and are
    /// slower to send without finding objects any better; the label map is made at this size too.
    /// </summary>
    public const int MaxLongEdge = 2048;

    /// <summary>At most this many objects are kept per room, so the editor's object list stays usable.</summary>
    public const int MaxObjects = 40;

    /// <summary>Objects covering less of the photo than this (after overlaps) are dropped as noise.</summary>
    public const double MinAreaFraction = 0.002;
}

/// <summary>The copy of a room photo that the AI providers see.</summary>
/// <param name="Jpeg">The image as JPEG bytes.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
public sealed record AnalysisImage(byte[] Jpeg, int Width, int Height);

/// <summary>One object the inventory step found, before it is outlined.</summary>
/// <param name="Label">What the object is, such as "velvet sofa".</param>
/// <param name="Category">The kind of object.</param>
/// <param name="Box">Roughly where the object is.</param>
/// <param name="Color">The object's main color as the AI saw it.</param>
/// <param name="Points">
/// A few spots on the object itself, for walls, floors and ceilings: they tell the outlining step
/// which surface is meant when its box also holds furniture. Empty when the AI gave none.
/// </param>
public sealed record InventoryItem(
    string Label, ObjectCategory Category, BoundingBox Box, HexColor Color, IReadOnlyList<ImagePoint>? Points = null);

/// <summary>A spot in the image, as fractions (0 to 1) of its width and height from the top-left corner.</summary>
/// <param name="X">Across.</param>
/// <param name="Y">Down.</param>
public sealed record ImagePoint(double X, double Y);

/// <summary>
/// The outline of one object: how much each pixel belongs to it, from 0 (not at all) to 255 (fully).
/// It may be smaller or larger than the analysis image; it is scaled to fit.
/// </summary>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="Coverage">One byte per pixel, row by row.</param>
public sealed record ObjectMask(int Width, int Height, byte[] Coverage);

/// <summary>An inventory item together with its outline.</summary>
/// <param name="Item">What the object is.</param>
/// <param name="Mask">Its outline.</param>
public sealed record SegmentedObject(InventoryItem Item, ObjectMask Mask);

/// <summary>The finished analysis: the label map and the objects that are visible in it.</summary>
/// <param name="Png">A grayscale PNG the size of the analysis image; each pixel's value is the index of the object it belongs to, 0 for none.</param>
/// <param name="Objects">The kept objects, indexed 1..N, with boxes and colors measured from the photo.</param>
public sealed record LabelMap(byte[] Png, IReadOnlyList<DetectedObject> Objects);

/// <summary>Which AI modes this API can use, and which one an upload gets when it doesn't choose.</summary>
/// <param name="Default">The mode of an upload that doesn't choose.</param>
/// <param name="CanChoose">Whether uploads may choose (Development only: production always uses the real AI).</param>
/// <param name="RealAvailable">Whether the real AI is set up (both API keys present).</param>
public sealed record AiAvailability(AiMode Default, bool CanChoose, bool RealAvailable)
{
    /// <summary>Whether an upload may use a mode.</summary>
    /// <param name="mode">The mode asked for.</param>
    public bool Allows(AiMode mode) => mode == Default || (CanChoose && (mode is AiMode.Fake || RealAvailable));
}

/// <summary>Hands out the AI providers of a mode: the fakes or Claude and SAM 3.</summary>
public interface IAiProviders
{
    /// <summary>Which modes can be used.</summary>
    AiAvailability Availability { get; }

    /// <summary>Step 1 of a mode.</summary>
    /// <param name="mode">Fake or real.</param>
    /// <exception cref="InvalidOperationException">The mode isn't set up.</exception>
    IObjectInventoryProvider Inventory(AiMode mode);

    /// <summary>Step 2 of a mode.</summary>
    /// <param name="mode">Fake or real.</param>
    /// <exception cref="InvalidOperationException">The mode isn't set up.</exception>
    ISegmentationProvider Segmentation(AiMode mode);
}

/// <summary>Step 1 of the analysis (Claude): lists the objects in the photo.</summary>
public interface IObjectInventoryProvider
{
    /// <summary>Lists the objects worth restyling in a room photo.</summary>
    /// <param name="image">The photo to look at.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The objects, or a failure the user can read when the photo isn't of a room.</returns>
    Task<Result<IReadOnlyList<InventoryItem>>> ListObjectsAsync(AnalysisImage image, CancellationToken cancellationToken);
}

/// <summary>Step 2 of the analysis (SAM 3): outlines each listed object.</summary>
public interface ISegmentationProvider
{
    /// <summary>Outlines the given objects in the photo.</summary>
    /// <param name="image">The photo the items were found in.</param>
    /// <param name="items">The objects to outline.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    /// <returns>One entry per item, in the same order; <c>null</c> where the object couldn't be outlined.</returns>
    Task<IReadOnlyList<ObjectMask?>> SegmentAsync(AnalysisImage image, IReadOnlyList<InventoryItem> items, CancellationToken cancellationToken);
}

/// <summary>The pixel work of the analysis: scaling the photo and building the label map.</summary>
public interface IRoomImaging
{
    /// <summary>Makes the copy of a stored photo that the AI providers see.</summary>
    /// <param name="photo">The stored room photo (JPEG).</param>
    /// <returns>The photo scaled to at most <see cref="AnalysisRules.MaxLongEdge"/>.</returns>
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
}

/// <summary>Whether a brush stroke adds pixels to the object or takes them away.</summary>
public enum OutlineBrushMode
{
    /// <summary>Every covered pixel becomes the object's, even if another object had it.</summary>
    Paint,
    /// <summary>Covered pixels of the object become unassigned; other objects are untouched.</summary>
    Erase,
}

/// <summary>Limits for brush strokes.</summary>
public static class OutlineStrokeRules
{
    /// <summary>The most points one stroke may have.</summary>
    public const int MaxPoints = 10_000;

    /// <summary>The smallest brush radius, in label map pixels.</summary>
    public const double MinRadius = 0.5;

    /// <summary>The largest brush radius, in label map pixels.</summary>
    public const double MaxRadius = 400;
}

/// <summary>A point of a stroke.</summary>
/// <param name="X">0..1 of the photo's width.</param>
/// <param name="Y">0..1 of the photo's height.</param>
public readonly record struct StrokePoint(double X, double Y);

/// <summary>One brush stroke the user drew on an object.</summary>
/// <param name="ObjectIndex">The object to change.</param>
/// <param name="ObjectLabel">Its name, for messages.</param>
/// <param name="Mode">Paint or erase.</param>
/// <param name="Radius">The brush radius, in label map pixels.</param>
/// <param name="Points">The path, at least one point; the brush is stamped along it.</param>
public sealed record OutlineStroke(int ObjectIndex, string ObjectLabel, OutlineBrushMode Mode, double Radius, IReadOnlyList<StrokePoint> Points);

/// <summary>Tells the browser that a room changed (live progress over SignalR).</summary>
public interface IRoomNotifier
{
    /// <summary>Sends the room's current state to everyone watching it.</summary>
    /// <param name="room">The changed room.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    Task RoomChangedAsync(Room room, CancellationToken cancellationToken);
}

/// <summary>Hands rooms to the background worker that analyzes them.</summary>
public interface IAnalysisScheduler
{
    /// <summary>Queues a room for analysis. Never blocks.</summary>
    /// <param name="roomId">The room to analyze.</param>
    void Schedule(Guid roomId);
}
