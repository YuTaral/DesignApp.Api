using DesignApp.Application.Helpers;
using DesignApp.Application.Interfaces;


namespace DesignApp.Application.UseCases;

/// <summary>
/// Moves the rooms and room plans a browser made as a guest into the account it just signed in to, so trying the
/// app first loses nothing.
/// </summary>
/// <param name="rooms">Loads and saves the rooms.</param>
/// <param name="plans">Loads and saves the room plans.</param>
public sealed class SaveGuestRooms(IRoomRepository rooms, IPlanRepository plans)
{
    /// <summary>Gives every room and room plan of the guest to the account.</summary>
    /// <param name="guestId">The browser's guest id; anything else is ignored.</param>
    /// <param name="userId">The account's user id.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many rooms moved.</returns>
    public async Task<int> HandleAsync(string? guestId, string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        if (guestId is null || !GuestIds.IsGuest(guestId) || GuestIds.IsGuest(userId))
        {
            return 0;
        }

        var owned = await rooms.ListByOwnerAsync(guestId, cancellationToken);
        var built = await plans.ListByOwnerAsync(guestId, cancellationToken);

        foreach (var room in owned)
        {
            room.TransferTo(userId);
        }

        // The room plans too, so their photos' rooms keep their plan and its design.
        foreach (var plan in built)
        {
            plan.TransferTo(userId);
        }

        if (owned.Count > 0)
        {
            await rooms.SaveChangesAsync(cancellationToken);
        }

        if (built.Count > 0)
        {
            await plans.SaveChangesAsync(cancellationToken);
        }

        return owned.Count;
    }
}
