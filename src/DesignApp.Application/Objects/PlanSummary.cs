using DesignApp.Domain.Entities;

namespace DesignApp.Application.Objects;

/// <summary>A built room in the list of the user's rooms: the plan, and how many rooms hold its photos.</summary>
/// <param name="Plan">The plan.</param>
/// <param name="Photos">How many rooms hold its photos (each made by one Generate).</param>
public sealed record PlanSummary(RoomPlan Plan, int Photos);
