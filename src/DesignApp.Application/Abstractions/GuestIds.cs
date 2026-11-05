namespace DesignApp.Application.Abstractions;

/// <summary>
/// Owner ids of visitors who haven't signed in: <c>guest:</c> and a random UUID, one per browser.
/// The prefix keeps them apart from account ids, so a guest id can never name an account.
/// </summary>
public static class GuestIds
{
    /// <summary>Starts every guest id.</summary>
    public const string Prefix = "guest:";

    /// <summary>A guest id for a new browser.</summary>
    public static string New() => Prefix + Guid.NewGuid().ToString("N");

    /// <summary>The guest id for a browser's token, or <c>null</c> when the token isn't one we hand out.</summary>
    /// <param name="token">The UUID the browser sent back.</param>
    public static string? FromToken(string? token) =>
        Guid.TryParseExact(token, "N", out var id) && id != Guid.Empty ? Prefix + id.ToString("N") : null;

    /// <summary>The token a browser keeps for a guest id.</summary>
    /// <param name="guestId">A guest id from <see cref="New"/>.</param>
    public static string ToToken(string guestId) => guestId[Prefix.Length..];

    /// <summary>Whether an owner id belongs to a guest.</summary>
    /// <param name="ownerId">The id.</param>
    public static bool IsGuest(string ownerId) => ownerId.StartsWith(Prefix, StringComparison.Ordinal);
}
