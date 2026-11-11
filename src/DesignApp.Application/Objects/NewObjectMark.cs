namespace DesignApp.Application.Objects;

/// <summary>An object the user marked by hand: the strokes, applied in order, and the index it will get.</summary>
/// <param name="Index">The label map value the marked pixels get.</param>
/// <param name="Strokes">The strokes, at least one.</param>
public sealed record NewObjectMark(int Index, IReadOnlyList<MarkStroke> Strokes);
