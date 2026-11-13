namespace DesignApp.Api.Requests;

/// <summary>The body of <c>POST /api/rooms/{id}/parts</c>.</summary>
/// <param name="BaseRevision">The label map version the editor shows; the parts are added to it.</param>
/// <param name="ObjectIndex">The object whose parts the AI looks for.</param>
/// <param name="Instructions">The user's own words for the AI on which parts to find (at most 500 characters); missing for none.</param>
public sealed record FindPartsRequest(int BaseRevision, int ObjectIndex, string? Instructions = null);
