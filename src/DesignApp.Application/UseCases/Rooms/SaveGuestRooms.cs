using DesignApp.Application.Helpers;
using DesignApp.Application.Interfaces;


namespace DesignApp.Application.UseCases;

/// <summary>
/// Moves the rooms a browser made as a guest into the account it just signed in to, so trying the
/// app first loses nothing.
/// </summary>
/// <param name="rooms">Loads and saves the rooms.</param>
public sealed class SaveGuestRooms(IRoomRepository rooms)
{
    /// <summary>Gives every room of the guest to the account.</summary>
    /// <param name="guestId">The browser's guest id; anything else is ignored.</param>
    /// <param name="userId">The account's user id.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many rooms moved.</returns>
    public async Task<int> HandleAsync(string? guestId, string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (guestId is null || !GuestIds.IsGuest(guestId) || GuestIds.IsGuest(userId))
            return 0;

        var owned = await rooms.ListByOwnerAsync(guestId, cancellationToken);
        foreach (var room in owned)
            room.TransferTo(userId);
        if (owned.Count > 0)
            await rooms.SaveChangesAsync(cancellationToken);
        return owned.Count;
    }
}
