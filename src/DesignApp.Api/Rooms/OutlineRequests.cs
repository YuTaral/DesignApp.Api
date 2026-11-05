using DesignApp.Application.Analysis;

namespace DesignApp.Api.Rooms;

/// <summary>The body of <c>POST /api/rooms/{id}/outline</c>: one brush stroke.</summary>
/// <param name="BaseRevision">The label map version the editor shows; the stroke is drawn on it.</param>
/// <param name="ObjectIndex">The object to change.</param>
/// <param name="Mode">Paint the object or erase it.</param>
/// <param name="Radius">The brush radius, in label map pixels.</param>
/// <param name="Points">The stroke's path, each point 0..1 of the photo's width and height.</param>
public sealed record OutlineStrokeRequest(int BaseRevision, int ObjectIndex, OutlineBrushMode Mode, double Radius, IReadOnlyList<StrokePoint>? Points);

/// <summary>The body of <c>PUT /api/rooms/{id}/labels/current</c>.</summary>
/// <param name="Revision">The stored label map version to make current.</param>
public sealed record LabelMapRevisionRequest(int Revision);
