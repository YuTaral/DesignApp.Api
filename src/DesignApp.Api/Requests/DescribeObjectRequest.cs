namespace DesignApp.Api.Requests;

/// <summary>The body of <c>POST /api/plans/{id}/describe</c>.</summary>
/// <param name="Description">The object in the user's words, such as "a round oak coffee table, about 90 cm".</param>
public sealed record DescribeObjectRequest(string? Description);
