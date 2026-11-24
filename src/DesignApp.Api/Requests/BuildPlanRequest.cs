namespace DesignApp.Api.Requests;

/// <summary>The optional body of <c>POST /api/rooms/{id}/plan</c>: the design the plan's photos go into.</summary>
/// <param name="DesignId">One of the user's designs with room for another room; none when missing.</param>
/// <param name="NewDesign">Without a design: the new design the plan's photos go into, made with the first one.</param>
public sealed record BuildPlanRequest(Guid? DesignId = null, NewDesignRequest? NewDesign = null);
