using DesignApp.Application.Abstractions;

namespace DesignApp.Api.Accounts;

/// <summary>
/// The cookie that gives each browser its own guest id until it signs in, so visitors who haven't
/// signed in never see each other's rooms.
/// </summary>
internal static class GuestCookie
{
    /// <summary>The cookie's name.</summary>
    public const string Name = "designapp.guest";

    /// <summary>Where a guest id issued during this request is kept, for later reads in the same request.</summary>
    private const string IssuedKey = "designapp.guest.issued";

    /// <summary>
    /// HTTP-only, so page scripts can't read it; Lax, so other sites can't send it with a POST;
    /// kept for a year.
    /// </summary>
    private static readonly CookieOptions Options = new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
        Path = "/",
        MaxAge = TimeSpan.FromDays(365),
    };

    /// <summary>The browser's guest id, or <c>null</c> when it has none (or sent something we didn't issue).</summary>
    /// <param name="http">The request.</param>
    public static string? Read(HttpContext http) =>
        // TryGetValue: a SignalR connection's copy of the request throws for a missing key.
        (http.Items.TryGetValue(IssuedKey, out var issued) ? issued as string : null)
        ?? GuestIds.FromToken(http.Request.Cookies[Name]);

    /// <summary>The browser's guest id, giving it a new one when it has none.</summary>
    /// <param name="http">The request. Once its response has started (a SignalR connection), no cookie can be set.</param>
    public static string GetOrIssue(HttpContext http)
    {
        if (Read(http) is { } existing)
            return existing;

        var id = GuestIds.New();
        http.Items[IssuedKey] = id;
        if (!http.Response.HasStarted)
            http.Response.Cookies.Append(Name, GuestIds.ToToken(id), Options);
        return id;
    }

    /// <summary>Forgets the browser's guest id, after its rooms moved into an account.</summary>
    /// <param name="http">The request.</param>
    public static void Clear(HttpContext http)
    {
        http.Items.Remove(IssuedKey);
        http.Response.Cookies.Delete(Name, Options);
    }
}
