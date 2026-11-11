using DesignApp.Domain.Objects;

namespace DesignApp.Application.Objects;

/// <summary>The result of marking an object by hand: the new label map and what was measured from its pixels.</summary>
/// <param name="LabelMapPng">The label map with the object's pixels set to its index.</param>
/// <param name="Box">The smallest box around the object's pixels.</param>
/// <param name="DominantColor">The photo's median color under the object.</param>
public sealed record MarkedObject(byte[] LabelMapPng, BoundingBox Box, HexColor DominantColor);
