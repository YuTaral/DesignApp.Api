using System.Security.Claims;
using DesignApp.Application.Abstractions;

namespace DesignApp.Api.Accounts;

/// <summary>
/// The current user of an HTTP request or SignalR connection: the signed-in account, else the
/// browser's guest (given a guest id on first use).
/// </summary>
/// <param name="accessor">Reaches the current request.</param>
internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private string? _id;

    /// <inheritdoc/>
    public string Id => _id ??= Resolve();

    /// <inheritdoc/>
    public bool IsSignedIn => Http.User.Identity?.IsAuthenticated == true;

    /// <summary>The request; there is always one where a use case asks who the user is.</summary>
    private HttpContext Http =>
        accessor.HttpContext ?? throw new InvalidOperationException("There is no current request to take the user from.");

    /// <summary>The account's id from the sign-in cookie, else the guest id.</summary>
    private string Resolve() =>
        IsSignedIn && Http.User.FindFirstValue(ClaimTypes.NameIdentifier) is { Length: > 0 } userId
            ? userId
            : GuestCookie.GetOrIssue(Http);
}
