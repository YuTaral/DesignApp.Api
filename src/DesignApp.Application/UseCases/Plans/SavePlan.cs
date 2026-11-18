using DesignApp.Application.Interfaces;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.UseCases;

/// <summary>Saves a built room as the user left it: its name, walls, colors, floor, finish, view and objects.</summary>
/// <param name="plans">Where plans are stored.</param>
/// <param name="currentUser">The signed-in user or guest.</param>
/// <param name="clock">The current time.</param>
public sealed class SavePlan(IPlanRepository plans, ICurrentUser currentUser, TimeProvider clock)
{
    /// <summary>Saves the plan.</summary>
    /// <param name="id">The plan's id.</param>
    /// <param name="name">Its name.</param>
    /// <param name="outline">The walls on the floor plan.</param>
    /// <param name="height">The ceiling height, in metres.</param>
    /// <param name="wallColor">The walls' color.</param>
    /// <param name="floorColor">The floor's color.</param>
    /// <param name="floorType">What the floor is made of.</param>
    /// <param name="view">The number of the wall the 3D view and the photo look at.</param>
    /// <param name="items">The objects.</param>
    /// <param name="corner">Where the camera stands to look at the wall in view.</param>
    /// <param name="finish">How the walls and ceiling are finished and lit; <c>null</c> keeps the plan's own.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The saved plan, or <c>null</c> when the user has no such plan.</returns>
    /// <exception cref="DomainException">The name, walls or objects are invalid.</exception>
    public async Task<RoomPlan?> HandleAsync(Guid id, string name, RoomOutline outline, double height,
        HexColor wallColor, HexColor floorColor, FloorType floorType, int view, IReadOnlyList<PlanItem> items, ViewCorner corner,
        RoomFinish? finish, CancellationToken cancellationToken)
    {
        var plan = await plans.FindAsync(id, currentUser.Id, cancellationToken);

        if (plan is null)
        {
            return null;
        }

        plan.Update(name, outline, height, wallColor, floorColor, floorType, view, items, clock.GetUtcNow(), corner, finish);
        await plans.SaveChangesAsync(cancellationToken);

        return plan;
    }
}
