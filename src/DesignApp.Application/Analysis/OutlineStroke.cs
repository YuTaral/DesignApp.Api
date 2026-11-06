using static DesignApp.Application.Constants;

namespace DesignApp.Application.Analysis;

/// <summary>One brush stroke the user drew on an object.</summary>
/// <param name="ObjectIndex">The object to change.</param>
/// <param name="ObjectLabel">Its name, for messages.</param>
/// <param name="Mode">Paint or erase.</param>
/// <param name="Radius">The brush radius, in label map pixels.</param>
/// <param name="Points">The path, at least one point; the brush is stamped along it.</param>
public sealed record OutlineStroke(int ObjectIndex, string ObjectLabel, OutlineBrushMode Mode, double Radius, IReadOnlyList<StrokePoint> Points);
