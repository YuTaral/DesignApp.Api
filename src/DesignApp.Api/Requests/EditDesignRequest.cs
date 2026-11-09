namespace DesignApp.Api.Requests;

/// <summary>Body of <c>PUT /api/designs/{id}</c>.</summary>
/// <param name="Name">The design's new name.</param>
/// <param name="Description">Its new description; empty or missing for none.</param>
internal sealed record EditDesignRequest(string? Name, string? Description);
