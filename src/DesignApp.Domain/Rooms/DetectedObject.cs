using DesignApp.Domain.Common;

namespace DesignApp.Domain.Rooms;

/// <summary>
/// One object found in a room photo. <see cref="Index"/> is the object's value in the label map,
/// so the browser can find the object under the cursor with a single pixel lookup.
/// </summary>
/// <param name="Index">The object's value in the label map; unique within the room.</param>
/// <param name="Label">What the object is, such as "sofa".</param>
/// <param name="Category">The kind of object.</param>
/// <param name="Box">Where the object is in the photo.</param>
/// <param name="DominantColor">The object's main color, used as the editor's starting color.</param>
public sealed record DetectedObject(int Index, string Label, ObjectCategory Category, BoundingBox Box, HexColor DominantColor);
