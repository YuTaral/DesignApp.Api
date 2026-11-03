using DesignApp.Domain.Common;

namespace DesignApp.Domain.Rooms;

/// <summary>
/// One object found in a room photo. <see cref="Index"/> is the object's value in the label map,
/// so the browser can find the object under the cursor with a single pixel lookup.
/// </summary>
public sealed record DetectedObject(int Index, string Label, ObjectCategory Category, BoundingBox Box, HexColor DominantColor);
