namespace DesignApp.Application.Objects;

/// <summary>An object the user marked by hand: the strokes, applied in order, and the index it will get.</summary>
/// <param name="Index">The label map value the marked pixels get.</param>
/// <param name="Strokes">The strokes, at least one.</param>
/// <param name="ParentIndex">For a part, the object it is a part of: only that object's pixels are taken. <c>null</c> for a whole object.</param>
/// <param name="ParentLabel">The parent's name, for messages.</param>
public sealed record NewObjectMark(int Index, IReadOnlyList<MarkStroke> Strokes, int? ParentIndex = null, string? ParentLabel = null);
