namespace DesignApp.Api.Responses;

/// <summary>The design a room of a plan's photos is in.</summary>
/// <param name="DesignId">The plan's design.</param>
public sealed record PlanDesignResponse(Guid DesignId);
