using DesignApp.Application.Helpers;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;

namespace DesignApp.Application.UseCases;

/// <summary>
/// Use case: a room of a plan's photos goes into the plan's own design, once; for a guest's photos after they sign in
/// (signed-in users' photos go there as they are made).
/// </summary>
/// <param name="rooms">The room database.</param>
/// <param name="plans">The plan database.</param>
/// <param name="planDesigns">Finds or makes the plan's design.</param>
/// <param name="currentUser">The signed-in user.</param>
public sealed class SaveRoomToPlanDesign(IRoomRepository rooms, IPlanRepository plans, PlanDesigns planDesigns, ICurrentUser currentUser)
{
    /// <summary>Saves the room into its plan's design.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The design's id, a failure the user can read, or <c>null</c> when the user has no such room.</returns>
    public async Task<Result<Guid>?> HandleAsync(Guid roomId, CancellationToken cancellationToken)
    {
        var room = await rooms.FindAsync(roomId, currentUser.Id, cancellationToken);

        if (room is null)
        {
            return null;
        }

        var plan = room.PlanId is Guid planId ? await plans.FindAsync(planId, currentUser.Id, cancellationToken) : null;

        if (plan is null)
        {
            return Result.Failure<Guid>(Constants.Messages.NotFromPlan);
        }

        var refusal = await planDesigns.AddAsync(plan, room, cancellationToken);

        return refusal is null ? plan.DesignId!.Value : Result.Failure<Guid>(refusal);
    }
}
