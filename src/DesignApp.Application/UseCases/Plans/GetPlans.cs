using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;

namespace DesignApp.Application.UseCases;

/// <summary>Lists the rooms the user builds, the most recently saved first, with how many photos each has made.</summary>
/// <param name="plans">Loads the plans.</param>
/// <param name="rooms">Loads the user's rooms, to count each plan's photos.</param>
/// <param name="currentUser">Whose plans are listed.</param>
public sealed class GetPlans(IPlanRepository plans, IRoomRepository rooms, ICurrentUser currentUser)
{
    /// <summary>Lists the plans.</summary>
    /// <param name="cancellationToken">Cancels the queries.</param>
    public async Task<IReadOnlyList<PlanSummary>> HandleAsync(CancellationToken cancellationToken)
    {
        var built = await plans.ListByOwnerAsync(currentUser.Id, cancellationToken);
        var owned = await rooms.ListByOwnerAsync(currentUser.Id, cancellationToken);
        var photos = owned.Where(r => r.PlanId is not null).GroupBy(r => r.PlanId!.Value).ToDictionary(g => g.Key, g => g.Count());

        return [.. built.Select(p => new PlanSummary(p, photos.GetValueOrDefault(p.Id)))];
    }
}
