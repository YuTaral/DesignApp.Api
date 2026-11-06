namespace DesignApp.Api.Designs;

/// <summary>Body of <c>POST /api/designs</c>.</summary>
/// <param name="RoomId">The design's first room.</param>
/// <param name="Name">What to call the design (1 to 80 characters).</param>
/// <param name="Description">An optional short description (up to 300 characters).</param>
/// <param name="Edits">The room's colors, at most one per object.</param>
internal sealed record CreateDesignRequest(Guid RoomId, string? Name, string? Description, List<EditRequest>? Edits);
