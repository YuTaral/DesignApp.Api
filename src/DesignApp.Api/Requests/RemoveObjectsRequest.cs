namespace DesignApp.Api.Requests;

/// <summary>The body of <c>POST /api/rooms/{id}/removals</c>.</summary>
/// <param name="BaseRevision">The label map version the editor shows; the objects are removed from it.</param>
/// <param name="ObjectIndexes">The objects to remove together, such as a sofa and the cushions on it.</param>
public sealed record RemoveObjectsRequest(int BaseRevision, IReadOnlyList<int>? ObjectIndexes);
