using DesignApp.Domain.Rooms;

namespace DesignApp.Application.Analysis;

/// <summary>The finished analysis: the label map and the objects that are visible in it.</summary>
/// <param name="Png">A grayscale PNG the size of the analysis image; each pixel's value is the index of the object it belongs to, 0 for none.</param>
/// <param name="Objects">The kept objects, indexed 1..N, with boxes and colors measured from the photo.</param>
public sealed record LabelMap(byte[] Png, IReadOnlyList<DetectedObject> Objects);
