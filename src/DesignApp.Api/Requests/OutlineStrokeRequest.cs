using DesignApp.Application.Objects;

using static DesignApp.Application.Constants;

namespace DesignApp.Api.Requests;

/// <summary>The body of <c>POST /api/rooms/{id}/outline</c>: one brush stroke.</summary>
/// <param name="BaseRevision">The label map version the editor shows; the stroke is drawn on it.</param>
/// <param name="ObjectIndex">The object to change.</param>
/// <param name="Mode">Paint the object or erase it.</param>
/// <param name="Radius">The brush radius, in label map pixels.</param>
/// <param name="Points">The stroke's path, each point 0..1 of the photo's width and height (a box's two corners, or a lasso).</param>
/// <param name="Shape">What the stroke covers; the brush when left out.</param>
public sealed record OutlineStrokeRequest(
    int BaseRevision, int ObjectIndex, OutlineBrushMode Mode, double Radius, IReadOnlyList<StrokePoint>? Points, StrokeShape Shape = StrokeShape.Brush);
