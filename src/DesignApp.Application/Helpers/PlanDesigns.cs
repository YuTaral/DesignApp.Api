using DesignApp.Application.Interfaces;
using DesignApp.Domain.Entities;

namespace DesignApp.Application.Helpers;

/// <summary>
/// The design a room plan's photos go into: the plan's own, made (named after the plan) the first time a room of its
/// photos is added, and made again if the user deleted it. Guests have no designs.
/// </summary>
/// <param name="designs">Where designs are stored.</param>
/// <param name="plans">Where plans are stored.</param>
/// <param name="clock">When a design changes.</param>
public sealed class PlanDesigns(IDesignRepository designs, IPlanRepository plans, TimeProvider clock)
{
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
