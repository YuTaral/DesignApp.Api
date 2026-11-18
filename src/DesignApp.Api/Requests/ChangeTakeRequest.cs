namespace DesignApp.Api.Requests;

/// <summary>The body of <c>POST /api/rooms/{id}/takes/{takeId}/change</c>.</summary>
/// <param name="Instructions">The change, in the user's words.</param>
public sealed record ChangeTakeRequest(string? Instructions);
