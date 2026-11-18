using DesignApp.Application.Objects;

namespace DesignApp.Api.Responses;

/// <summary>A plan read from a room's photo, as the API returns it.</summary>
/// <param name="Plan">The new plan.</param>
/// <param name="LeftOut">The names of the objects seen in the photo but left out (they fit nowhere, or weren't valid objects).</param>
public sealed record PhotoPlanResponse(PlanResponse Plan, IReadOnlyList<string> LeftOut)
{
    /// <summary>Maps the outcome to its API shape.</summary>
    /// <param name="outcome">The plan and what was left out.</param>
    public static PhotoPlanResponse From(PhotoPlanOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return new(PlanResponse.From(outcome.Plan), outcome.LeftOut);
    }
}
