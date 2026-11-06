namespace DesignApp.Application.Abstractions;

/// <summary>
/// Owner ids of visitors who haven't signed in: <c>guest:</c> and a random UUID, one per browser.
/// The prefix keeps them apart from account ids, so a guest id can never name an account.
/// </summary>
public static class GuestIds
{
    /// <summary>A guest id for a new browser.</summary>
    public static string New() => Constants.Guests.Prefix + Guid.NewGuid().ToString(Constants.Guests.TokenFormat);

    /// <summary>The guest id for a browser's token, or <c>null</c> when the token isn't one we hand out.</summary>
    /// <param name="token">The UUID the browser sent back.</param>
    public static string? FromToken(string? token) =>
        Guid.TryParseExact(token, Constants.Guests.TokenFormat, out var id) && id != Guid.Empty
            ? Constants.Guests.Prefix + id.ToString(Constants.Guests.TokenFormat)
            : null;

    /// <summary>The token a browser keeps for a guest id.</summary>
    /// <param name="guestId">A guest id from <see cref="New"/>.</param>
    public static string ToToken(string guestId) => guestId[Constants.Guests.Prefix.Length..];

    /// <summary>Whether an owner id belongs to a guest.</summary>
    /// <param name="ownerId">The id.</param>
    public static bool IsGuest(string ownerId) => ownerId.StartsWith(Constants.Guests.Prefix, StringComparison.Ordinal);
}
