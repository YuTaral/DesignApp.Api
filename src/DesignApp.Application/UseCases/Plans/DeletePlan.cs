using DesignApp.Application.Interfaces;

namespace DesignApp.Application.UseCases;

/// <summary>
/// Deletes a room the user builds. The rooms holding its photos stay, with their designs: they only let go of the plan,
/// so they no longer show its 3D view.
/// </summary>
/// <param name="plans">Loads and deletes the plan.</param>
/// <param name="rooms">Finds the rooms holding its photos.</param>
/// <param name="currentUser">Whose plans may be deleted.</param>
public sealed class DeletePlan(IPlanRepository plans, IRoomRepository rooms, ICurrentUser currentUser)
{
    /// <summary>Deletes the plan.</summary>
    /// <param name="id">The plan.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>False when the user has no such plan.</returns>
    public async Task<bool> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await plans.FindAsync(id, currentUser.Id, cancellationToken);

        if (plan is null)
        {
            return false;
        }

        var photos = await rooms.ListByPlanAsync(plan.Id, currentUser.Id, cancellationToken);

        foreach (var room in photos)
        {
            room.LeavePlan();
        }

        plans.Remove(plan);
        await plans.SaveChangesAsync(cancellationToken);

        return true;
    }
}
