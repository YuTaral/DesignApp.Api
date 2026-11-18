using DesignApp.Domain.Entities;

namespace DesignApp.Application.Objects;

/// <summary>A plan read from a room photo, and what was seen but left out (it fit nowhere, or wasn't a valid object).</summary>
/// <param name="Plan">The new plan.</param>
/// <param name="LeftOut">The names of the objects left out.</param>
public sealed record PhotoPlanOutcome(RoomPlan Plan, IReadOnlyList<string> LeftOut);
