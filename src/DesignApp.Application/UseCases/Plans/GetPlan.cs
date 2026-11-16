using DesignApp.Application.Interfaces;
using DesignApp.Domain.Entities;

namespace DesignApp.Application.UseCases;

/// <summary>Reads one of the current user's built rooms.</summary>
/// <param name="plans">Where plans are stored.</param>
/// <param name="currentUser">The signed-in user or guest.</param>
public sealed class GetPlan(IPlanRepository plans, ICurrentUser currentUser)
{
    /// <summary>Finds the plan.</summary>
    /// <param name="id">The plan's id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The plan, or <c>null</c> when the user has no such plan.</returns>
    public Task<RoomPlan?> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        return plans.FindAsync(id, currentUser.Id, cancellationToken);
    }
}
