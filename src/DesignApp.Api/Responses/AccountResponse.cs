using System.Security.Claims;

namespace DesignApp.Api.Responses;

/// <summary>Who is signed in.</summary>
/// <param name="SignedIn">Whether an account is signed in; otherwise the browser is a guest.</param>
/// <param name="Email">The account's email, when signed in.</param>
internal sealed record AccountResponse(bool SignedIn, string? Email)
{
    /// <summary>Reads the signed-in account from the request's user.</summary>
    /// <param name="user">The request's user.</param>
    public static AccountResponse From(ClaimsPrincipal user)
    {
        return user.Identity?.IsAuthenticated == true
            ? new AccountResponse(true, user.FindFirstValue(ClaimTypes.Email) ?? user.Identity.Name)
            : new AccountResponse(false, null);
    }
}
