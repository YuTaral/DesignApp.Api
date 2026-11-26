using DesignApp.Application.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Api.Responses;

/// <summary>A built room in the list of the user's rooms.</summary>
/// <param name="Id">Its id.</param>
/// <param name="Name">Its name.</param>
/// <param name="RoomType">What kind of room it is.</param>
/// <param name="Width">The width, in metres.</param>
/// <param name="Length">The length, in metres.</param>
/// <param name="Items">How many objects are placed in it.</param>
/// <param name="Photos">How many rooms hold its photos.</param>
/// <param name="DesignId">The design its photos go into; <c>null</c> until there is one.</param>
/// <param name="UpdatedAt">When it was last saved.</param>
public sealed record PlanSummaryResponse(Guid Id, string Name, RoomType RoomType, double Width, double Length, int Items, int Photos, Guid? DesignId, DateTimeOffset UpdatedAt)
{
    /// <summary>Maps a plan in the list to its API shape.</summary>
    /// <param name="summary">The plan and its photo count.</param>
    public static PlanSummaryResponse From(PlanSummary summary)
    {
        var plan = summary.Plan;

        return new PlanSummaryResponse(plan.Id, plan.Name, plan.RoomType, plan.Width, plan.Length, plan.Items.Count, summary.Photos, plan.DesignId, plan.UpdatedAt);
    }
}
