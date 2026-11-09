namespace DesignApp.Api;

/// <summary>Body of <c>GET /api/status</c>.</summary>
/// <param name="Name">The service's display name.</param>
/// <param name="Environment">The hosting environment, such as "Development".</param>
internal sealed record ApiStatus(string Name, string Environment);
