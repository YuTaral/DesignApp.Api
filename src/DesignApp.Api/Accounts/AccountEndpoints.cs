using System.Security.Claims;
using DesignApp.Application.Accounts;
using DesignApp.Infrastructure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace DesignApp.Api.Accounts;

/// <summary>The <c>/api/account</c> endpoints: sign up, sign in, sign out, and who is signed in.</summary>
internal static class AccountEndpoints
{
    /// <summary>Shown for an unknown email or a wrong password alike, so it can't reveal which accounts exist.</summary>
    internal const string WrongCredentials = "Wrong email or password.";

    /// <summary>Maps the account endpoints under <c>/account</c>.</summary>
    /// <param name="api">The <c>/api</c> route group.</param>
    /// <returns>The same group, for chaining.</returns>
    public static RouteGroupBuilder MapAccountEndpoints(this RouteGroupBuilder api)
    {
        var account = api.MapGroup("/account").WithTags("Account");

        account.MapGet("/", GetAsync)
            .WithName("GetAccount")
            .WithSummary("Says whether the browser is signed in, and as whom.");

        account.MapPost("/sign-up", SignUpAsync)
            .WithName("SignUp")
            .WithSummary("Creates an account with an email and a password, signs in, and moves this browser's guest rooms into it.")
            .RequireRateLimiting(RateLimitPolicies.Accounts);

        account.MapPost("/sign-in", SignInAsync)
            .WithName("SignIn")
            .WithSummary("Signs in with an email and a password, and moves this browser's guest rooms into the account.")
            .WithDescription("After 5 wrong passwords in a row the account is locked for 5 minutes.")
            .RequireRateLimiting(RateLimitPolicies.Accounts);

        account.MapPost("/sign-out", SignOutAsync)
            .WithName("SignOut")
            .WithSummary("Signs out. The browser continues as a new guest.");

        return api;
    }

    /// <summary><c>GET /api/account</c>.</summary>
    /// <param name="http">The request.</param>
    private static Ok<AccountResponse> GetAsync(HttpContext http) => TypedResults.Ok(AccountResponse.From(http.User));

    /// <summary><c>POST /api/account/sign-up</c>.</summary>
    /// <param name="request">The email and password.</param>
    /// <param name="users">Creates the account.</param>
    /// <param name="signIn">Sets the sign-in cookie.</param>
    /// <param name="claimGuestRooms">Moves the guest's rooms.</param>
    /// <param name="http">The request, for the guest cookie.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the account, or 400 with what to fix.</returns>
    private static async Task<Results<Ok<AccountResponse>, ProblemHttpResult>> SignUpAsync(
        CredentialsRequest request, UserManager<AppUser> users, SignInManager<AppUser> signIn,
        ClaimGuestRooms claimGuestRooms, HttpContext http, CancellationToken cancellationToken)
    {
        if (Missing(request) is { } missing)
            return TypedResults.Problem(missing, statusCode: StatusCodes.Status400BadRequest, title: "Couldn't sign up");

        var email = request.Email!.Trim();
        var user = new AppUser { UserName = email, Email = email };
        var created = await users.CreateAsync(user, request.Password!);
        if (!created.Succeeded)
            return TypedResults.Problem(Describe(created.Errors), statusCode: StatusCodes.Status400BadRequest, title: "Couldn't sign up");

        await signIn.SignInAsync(user, isPersistent: true);
        await ClaimAsync(claimGuestRooms, http, user, cancellationToken);
        return TypedResults.Ok(new AccountResponse(true, email));
    }

    /// <summary><c>POST /api/account/sign-in</c>.</summary>
    /// <param name="request">The email and password.</param>
    /// <param name="users">Finds the account.</param>
    /// <param name="signIn">Checks the password and sets the sign-in cookie.</param>
    /// <param name="claimGuestRooms">Moves the guest's rooms.</param>
    /// <param name="http">The request, for the guest cookie.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the account, 400 for wrong details, or 429 while the account is locked.</returns>
    private static async Task<Results<Ok<AccountResponse>, ProblemHttpResult>> SignInAsync(
        CredentialsRequest request, UserManager<AppUser> users, SignInManager<AppUser> signIn,
        ClaimGuestRooms claimGuestRooms, HttpContext http, CancellationToken cancellationToken)
    {
        if (Missing(request) is { } missing)
            return TypedResults.Problem(missing, statusCode: StatusCodes.Status400BadRequest, title: "Couldn't sign in");

        var user = await users.FindByEmailAsync(request.Email!.Trim());
        if (user is null)
            return TypedResults.Problem(WrongCredentials, statusCode: StatusCodes.Status400BadRequest, title: "Couldn't sign in");

        var result = await signIn.PasswordSignInAsync(user, request.Password!, isPersistent: true, lockoutOnFailure: true);
        if (result.IsLockedOut)
            return TypedResults.Problem("Too many wrong passwords. Try again in a few minutes.",
                statusCode: StatusCodes.Status429TooManyRequests, title: "Couldn't sign in");
        if (!result.Succeeded)
            return TypedResults.Problem(WrongCredentials, statusCode: StatusCodes.Status400BadRequest, title: "Couldn't sign in");

        await ClaimAsync(claimGuestRooms, http, user, cancellationToken);
        return TypedResults.Ok(new AccountResponse(true, user.Email));
    }

    /// <summary><c>POST /api/account/sign-out</c>.</summary>
    /// <param name="signIn">Removes the sign-in cookie.</param>
    /// <returns>204.</returns>
    private static async Task<NoContent> SignOutAsync(SignInManager<AppUser> signIn)
    {
        await signIn.SignOutAsync();
        return TypedResults.NoContent();
    }

    /// <summary>Moves the browser's guest rooms into the account and forgets the guest id.</summary>
    /// <param name="claimGuestRooms">The use case.</param>
    /// <param name="http">The request.</param>
    /// <param name="user">The account just signed in to.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    private static async Task ClaimAsync(ClaimGuestRooms claimGuestRooms, HttpContext http, AppUser user, CancellationToken cancellationToken)
    {
        if (GuestCookie.Read(http) is not { } guestId)
            return;
        await claimGuestRooms.HandleAsync(guestId, user.Id, cancellationToken);
        GuestCookie.Clear(http);
    }

    /// <summary>What's missing from the form, or <c>null</c> when both fields are filled in.</summary>
    /// <param name="request">The form.</param>
    private static string? Missing(CredentialsRequest request) =>
        string.IsNullOrWhiteSpace(request.Email) ? "Enter your email."
        : string.IsNullOrEmpty(request.Password) ? "Enter your password."
        : null;

    /// <summary>Identity's errors in the app's words.</summary>
    /// <param name="errors">Why the account couldn't be created.</param>
    private static string Describe(IEnumerable<IdentityError> errors) => string.Join(" ", errors
        .Select(e => e.Code switch
        {
            "DuplicateUserName" or "DuplicateEmail" => "An account with this email already exists. Sign in instead.",
            "InvalidEmail" or "InvalidUserName" => "Enter a valid email address.",
            "PasswordTooShort" => $"Use a password of at least {AccountSetup.MinPasswordLength} characters.",
            _ => e.Description,
        })
        .Distinct());
}

/// <summary>Body of <c>POST /api/account/sign-up</c> and <c>/sign-in</c>.</summary>
/// <param name="Email">The account's email.</param>
/// <param name="Password">The password.</param>
internal sealed record CredentialsRequest(string? Email, string? Password);

/// <summary>Who is signed in.</summary>
/// <param name="SignedIn">Whether an account is signed in; otherwise the browser is a guest.</param>
/// <param name="Email">The account's email, when signed in.</param>
internal sealed record AccountResponse(bool SignedIn, string? Email)
{
    /// <summary>Reads the signed-in account from the request's user.</summary>
    /// <param name="user">The request's user.</param>
    public static AccountResponse From(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
            ? new AccountResponse(true, user.FindFirstValue(ClaimTypes.Email) ?? user.Identity.Name)
            : new AccountResponse(false, null);
}
