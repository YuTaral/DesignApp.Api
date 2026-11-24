using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;

namespace DesignApp.Application.Helpers;

/// <summary>
/// The design a room plan's photos go into: the one the user started the plan from (Add room on a design's page), or
/// the plan's own, made (named after the plan) the first time a room of its photos is added, and made again if the user
/// deleted it. Guests have no designs.
/// </summary>
/// <param name="designs">Where designs are stored.</param>
/// <param name="plans">Where plans are stored.</param>
/// <param name="clock">When a design changes.</param>
public sealed class PlanDesigns(IDesignRepository designs, IPlanRepository plans, TimeProvider clock)
{
    /// <summary>
    /// The design a new plan's photos are to go into, when the user starts the plan from one: it must be the user's and
    /// have room for another room. Checked before the plan is made (and before any AI call).
    /// </summary>
    /// <param name="designId">The design asked for; none when missing.</param>
    /// <param name="ownerId">The user making the plan.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The design's id (none when none was asked for), or why it can't be used.</returns>
    public async Task<Result<Guid?>> ForNewPlanAsync(Guid? designId, string ownerId, CancellationToken cancellationToken)
    {
        if (designId is not Guid id)
        {
            return Result.Success<Guid?>(null);
        }

        var design = await designs.FindAsync(id, ownerId, cancellationToken);

        if (design is null)
        {
            return Result.Failure<Guid?>(Constants.Messages.PlanDesignMissing);
        }

        return design.Rooms.Count >= Domain.Constants.Designs.MaxRooms
            ? Result.Failure<Guid?>(Constants.Messages.PlanDesignAlreadyFull(design.Name))
            : Result.Success<Guid?>(id);
    }

    /// <summary>Puts a room of the plan's photos into the plan's design, once.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="room">The room, already saved, with the plan's owner.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns><c>null</c> when the room is in the design (or the owner is a guest), else why it couldn't be added.</returns>
    public async Task<string?> AddAsync(RoomPlan plan, Room room, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(room);

        if (GuestIds.IsGuest(room.OwnerId))
        {
            return null;
        }

        var design = plan.DesignId is Guid id ? await designs.FindAsync(id, plan.OwnerId, cancellationToken) : null;
        var now = clock.GetUtcNow();

        if (design?.Rooms.Any(r => r.RoomId == room.Id) == true)
        {
            return null;
        }

        if (design is not null && design.Rooms.Count >= Domain.Constants.Designs.MaxRooms)
        {
            return Constants.Messages.PlanDesignFull(design.Name);
        }

        if (design is null)
        {
            design = Design.Create(room, plan.OwnerId, NameOf(plan), null, [], now);
            designs.Add(design);
            plan.UseDesign(design.Id);
        }
        else
        {
            design.AddRoom(room, now);
        }

        await designs.SaveChangesAsync(cancellationToken);
        await plans.SaveChangesAsync(cancellationToken);

        return null;
    }

    /// <summary>A new design's name: the plan's, or its room type in words ("Living room").</summary>
    /// <param name="plan">The plan.</param>
    private static string NameOf(RoomPlan plan)
    {
        var words = PlanPrompt.Words(plan.RoomType.ToString());
        var name = string.IsNullOrWhiteSpace(plan.Name) ? char.ToUpperInvariant(words[0]) + words[1..] : plan.Name.Trim();

        return name.Length > Domain.Constants.Designs.MaxNameLength ? name[..Domain.Constants.Designs.MaxNameLength] : name;
    }
}
