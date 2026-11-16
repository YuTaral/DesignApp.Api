using DesignApp.Application.Helpers;
using DesignApp.Application.Interfaces;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.UseCases;

/// <summary>
/// Starts a room the user builds from scratch: its type and walls, and, unless the user wants an empty room, a starting
/// layout of furniture, windows and a door sized for it (<see cref="StarterLayout"/>).
/// </summary>
/// <param name="plans">Where plans are stored.</param>
/// <param name="currentUser">The signed-in user or guest.</param>
/// <param name="clock">The current time.</param>
/// <param name="catalog">The objects each room type starts with.</param>
public sealed class CreatePlan(IPlanRepository plans, ICurrentUser currentUser, TimeProvider clock, IObjectCatalog catalog)
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
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The new plan.</returns>
    /// <exception cref="DomainException">The name, type or walls are invalid.</exception>
    public async Task<RoomPlan> HandleAsync(string name, RoomType roomType, double width, double length, double height,
        HexColor wallColor, HexColor floorColor, FloorType floorType, AiMode aiMode, bool furnish, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var plan = RoomPlan.Create(Guid.CreateVersion7(now), currentUser.Id, name, roomType, width, length, height, wallColor, floorColor, floorType, aiMode, now);
        if (furnish)
        {
            var items = StarterLayout.For(roomType, width, length, height, catalog.ForRoom(roomType));
            plan.Update(plan.Name, plan.Outline, height, wallColor, floorColor, floorType, plan.View, items, now, plan.Corner);
        }
        plans.Add(plan);
        await plans.SaveChangesAsync(cancellationToken);
        return plan;
    }
}
