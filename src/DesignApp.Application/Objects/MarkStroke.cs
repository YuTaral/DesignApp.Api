using static DesignApp.Application.Constants;

namespace DesignApp.Application.Objects;

/// <summary>One brush stroke of an object the user marks by hand.</summary>
/// <param name="Mode"><see cref="OutlineBrushMode.Paint"/> marks the covered pixels, <see cref="OutlineBrushMode.Erase"/> unmarks them.</param>
/// <param name="Radius">The brush radius, in label map pixels (it changes with the brush size and the zoom).</param>
/// <param name="Points">The path, at least one point, each 0..1 of the photo; the brush is stamped along it.</param>
public sealed record MarkStroke(OutlineBrushMode Mode, double Radius, IReadOnlyList<StrokePoint> Points);
