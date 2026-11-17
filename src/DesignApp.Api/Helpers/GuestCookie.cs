using DesignApp.Application.Helpers;


namespace DesignApp.Api.Helpers;

/// <summary>
/// The cookie that gives each browser its own guest id until it signs in, so visitors who haven't
/// signed in never see each other's rooms.
/// </summary>
internal static class GuestCookie
{
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
        Path = Constants.Cookies.Path,
        MaxAge = Constants.Cookies.GuestLifetime,
    };

    /// <summary>The browser's guest id, or <c>null</c> when it has none (or sent something we didn't issue).</summary>
    /// <param name="http">The request.</param>
    public static string? Read(HttpContext http)
    {
        // TryGetValue: a SignalR connection's copy of the request throws for a missing key.
        var issued = http.Items.TryGetValue(Constants.Cookies.GuestIssuedItem, out var value) ? value as string : null;

        return issued ?? GuestIds.FromToken(http.Request.Cookies[Constants.Cookies.Guest]);
    }

    /// <summary>The browser's guest id, giving it a new one when it has none.</summary>
    /// <param name="http">The request. Once its response has started (a SignalR connection), no cookie can be set.</param>
    public static string GetOrIssue(HttpContext http)
    {
        if (Read(http) is { } existing)
        {
            return existing;
        }

        var id = GuestIds.New();

        http.Items[Constants.Cookies.GuestIssuedItem] = id;

        if (!http.Response.HasStarted)
        {
            http.Response.Cookies.Append(Constants.Cookies.Guest, GuestIds.ToToken(id), Options);
        }

        return id;
    }

    /// <summary>Forgets the browser's guest id, after its rooms moved into an account.</summary>
    /// <param name="http">The request.</param>
    public static void Clear(HttpContext http)
    {
        http.Items.Remove(Constants.Cookies.GuestIssuedItem);
        http.Response.Cookies.Delete(Constants.Cookies.Guest, Options);
    }
}
