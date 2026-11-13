namespace DesignApp.Api.Requests;

/// <summary>The body of <c>POST /api/rooms/{id}/merges</c>.</summary>
/// <param name="BaseRevision">The label map version the editor shows; the part is merged on it.</param>
/// <param name="ObjectIndex">The part to merge back into the object it is a part of.</param>
public sealed record MergePartRequest(int BaseRevision, int ObjectIndex);
