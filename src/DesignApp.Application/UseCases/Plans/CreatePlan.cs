using DesignApp.Application.Helpers;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.UseCases;

/// <summary>
/// Starts a room the user builds from scratch: its type and walls, and, unless the user wants an empty room, a starting
/// layout of furniture, windows and a door sized for it (<see cref="StarterLayout"/>). Started from a design (Add room on
/// its page), the room's photos go into that design.
/// </summary>
/// <param name="plans">Where plans are stored.</param>
/// <param name="currentUser">The signed-in user or guest.</param>
/// <param name="clock">The current time.</param>
/// <param name="catalog">The objects each room type starts with.</param>
/// <param name="planDesigns">Checks the design the room is started from.</param>
public sealed class CreatePlan(IPlanRepository plans, ICurrentUser currentUser, TimeProvider clock, IObjectCatalog catalog, PlanDesigns planDesigns)
{
    /// <summary>Creates the plan.</summary>
    /// <param name="name">Its name (may be empty).</param>
    /// <param name="roomType">What kind of room it is.</param>
    /// <param name="width">The width, in metres.</param>
    /// <param name="length">The length, in metres.</param>
    /// <param name="height">The ceiling height, in metres.</param>
    /// <param name="wallColor">The walls' color.</param>
    /// <param name="floorColor">The floor's color.</param>
    /// <param name="floorType">What the floor is made of.</param>
    /// <param name="aiMode">The AI its photos are made with.</param>
    /// <param name="furnish">Whether it starts with a suggested layout; otherwise it is empty.</param>
    /// <param name="finish">
    /// How the walls and ceiling are finished and lit; when <c>null</c>, one suited to the room type for a furnished room,
    /// else <see cref="RoomFinish.Default"/>.
    /// </param>
    /// <param name="designId">The design the room is started from, whose photos go into it; none when missing.</param>
    /// <param name="newDesign">Without a design: the new design its photos go into, made with the first one.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The new plan, or why the design can't take it (not the user's, or full).</returns>
    /// <exception cref="DomainException">The name, type or walls are invalid.</exception>
    public async Task<Result<RoomPlan>> HandleAsync(string name, RoomType roomType, double width, double length, double height,
        HexColor wallColor, HexColor floorColor, FloorType floorType, AiMode aiMode, bool furnish, RoomFinish? finish, Guid? designId,
        DesignDetails? newDesign, CancellationToken cancellationToken)
    {
        var design = await planDesigns.ForNewPlanAsync(designId, currentUser.Id, cancellationToken);

        if (!design.Succeeded)
        {
            return Result.Failure<RoomPlan>(design.Error);
        }

        var now = clock.GetUtcNow();
        var plan = RoomPlan.Create(Guid.CreateVersion7(now), currentUser.Id, name, roomType, width, length, height, wallColor, floorColor, floorType, aiMode, now);
        var items = furnish ? StarterLayout.For(roomType, width, length, height, catalog.ForRoom(roomType)) : [];
        var look = finish ?? (furnish ? StarterLayout.FinishFor(roomType) : RoomFinish.Default);

        plan.Update(plan.Name, plan.Outline, height, wallColor, floorColor, floorType, plan.Viewpoint, items, now, look);
        plan.ChooseDesign(design.Value, newDesign);
        plans.Add(plan);
        await plans.SaveChangesAsync(cancellationToken);

        return plan;
    }
}
