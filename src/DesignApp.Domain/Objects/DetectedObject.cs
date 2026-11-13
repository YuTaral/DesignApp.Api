using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Objects;

/// <summary>
/// One object found in a room photo. <see cref="Index"/> is the object's value in the label map,
/// so the browser can find the object under the cursor with a single pixel lookup.
/// </summary>
/// <param name="Index">The object's value in the label map; unique within the room.</param>
/// <param name="Label">What the object is, such as "sofa".</param>
/// <param name="Category">The kind of object.</param>
/// <param name="Box">Where the object is in the photo.</param>
/// <param name="DominantColor">The object's main color, used as the editor's starting color.</param>
/// <param name="MarkedByHand">Whether the user marked it with a brush (the AI missed it), rather than the AI finding it.</param>
/// <param name="ParentIndex">
/// For a part of another object (a chair's seat), that object's index; <c>null</c> for a whole object. A part's pixels
/// were the parent's, and a part has no parts of its own.
/// </param>
public sealed record DetectedObject(
    int Index, string Label, ObjectCategory Category, BoundingBox Box, HexColor DominantColor, bool MarkedByHand = false, int? ParentIndex = null);
