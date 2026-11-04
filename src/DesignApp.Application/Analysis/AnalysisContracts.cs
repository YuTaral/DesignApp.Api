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
public sealed record InventoryItem(string Label, ObjectCategory Category, BoundingBox Box, HexColor Color);

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
    /// on a sofa on the floor), objects left with almost no visible pixels are dropped, and each kept
    /// object's box and main color are measured from its visible pixels.
    /// </summary>
    /// <param name="image">The analysis image the outlines belong to.</param>
    /// <param name="objects">The outlined objects.</param>
    LabelMap BuildLabelMap(AnalysisImage image, IReadOnlyList<SegmentedObject> objects);
}

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
