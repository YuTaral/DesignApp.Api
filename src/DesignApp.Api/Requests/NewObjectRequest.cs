using DesignApp.Application.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Api.Requests;

/// <summary>The body of <c>POST /api/rooms/{id}/objects</c>: an object the user marked by hand.</summary>
/// <param name="BaseRevision">The label map version the editor shows; the mark is applied to it.</param>
/// <param name="Label">The object's name, such as "floor lamp".</param>
/// <param name="Category">The object's type.</param>
/// <param name="Strokes">The strokes, in order, each with its brush radius: Paint marks the covered pixels, Erase unmarks them.</param>
public sealed record NewObjectRequest(int BaseRevision, string? Label, ObjectCategory Category, IReadOnlyList<MarkStroke>? Strokes);
