using DesignApp.Api.Helpers;
using DesignApp.Api.Requests;
using DesignApp.Api.Responses;
using DesignApp.Application.UseCases;
using DesignApp.Infrastructure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace DesignApp.Api.Endpoints;

/// <summary>The <c>/api/account</c> endpoints: sign up, sign in, sign out, and who is signed in.</summary>
internal static class AccountEndpoints
{
    /// <summary>Maps the account endpoints under <c>/account</c>.</summary>
    /// <param name="api">The <c>/api</c> route group.</param>
    /// <returns>The same group, for chaining.</returns>
    public static RouteGroupBuilder MapAccountEndpoints(this RouteGroupBuilder api)
    {
        var account = api.MapGroup(Constants.Routes.Account).WithTags(Constants.OpenApi.AccountTag);

        account.MapGet(Constants.Routes.Root, GetAsync)
            .WithName("GetAccount")
            .WithSummary("Says whether the browser is signed in, and as whom.");

        account.MapPost(Constants.Routes.SignUp, SignUpAsync)
            .WithName("SignUp")
            .WithSummary("Creates an account with an email and a password, signs in, and moves this browser's guest rooms into it.")
            .RequireRateLimiting(Constants.RateLimits.AccountsPolicy);

        account.MapPost(Constants.Routes.SignIn, SignInAsync)
            .WithName("SignIn")
            .WithSummary("Signs in with an email and a password, and moves this browser's guest rooms into the account.")
            .WithDescription($"After {Constants.Accounts.MaxFailedAttempts} wrong passwords in a row the account is locked for {Constants.Accounts.LockoutTime.TotalMinutes} minutes.")
            .RequireRateLimiting(Constants.RateLimits.AccountsPolicy);

        account.MapPost(Constants.Routes.SignOut, SignOutAsync)
            .WithName("SignOut")
            .WithSummary("Signs out. The browser continues as a new guest.");

        return api;
    }

    /// <summary><c>GET /api/account</c>.</summary>
    /// <param name="http">The request.</param>
    private static Ok<AccountResponse> GetAsync(HttpContext http)
    {
        return TypedResults.Ok(AccountResponse.From(http.User));
    }

    /// <summary><c>POST /api/account/sign-up</c>.</summary>
    /// <param name="request">The email and password.</param>
    /// <param name="users">Creates the account.</param>
    /// <param name="signIn">Sets the sign-in cookie.</param>
    /// <param name="saveGuestRooms">Moves the guest's rooms.</param>
    /// <param name="http">The request, for the guest cookie.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the account, or 400 with what to fix.</returns>
    private static async Task<Results<Ok<AccountResponse>, ProblemHttpResult>> SignUpAsync(
        CredentialsRequest request, UserManager<AppUser> users, SignInManager<AppUser> signIn,
        SaveGuestRooms saveGuestRooms, HttpContext http, CancellationToken cancellationToken)
    {
        if (Missing(request) is { } missing)
        {
            return Problems.BadRequest(missing, Constants.Titles.SignUpFailed);
        }

        var email = request.Email!.Trim();
        var user = new AppUser { UserName = email, Email = email };
        var created = await users.CreateAsync(user, request.Password!);

        if (!created.Succeeded)
        {
            return Problems.BadRequest(Describe(created.Errors), Constants.Titles.SignUpFailed);
        }

        await signIn.SignInAsync(user, isPersistent: true);
        await SaveGuestRoomsAsync(saveGuestRooms, http, user, cancellationToken);

        return TypedResults.Ok(new AccountResponse(true, email));
    }

    /// <summary><c>POST /api/account/sign-in</c>.</summary>
    /// <param name="request">The email and password.</param>
    /// <param name="users">Finds the account.</param>
    /// <param name="signIn">Checks the password and sets the sign-in cookie.</param>
    /// <param name="saveGuestRooms">Moves the guest's rooms.</param>
    /// <param name="http">The request, for the guest cookie.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the account, 400 for wrong details, or 429 while the account is locked.</returns>
    private static async Task<Results<Ok<AccountResponse>, ProblemHttpResult>> SignInAsync(
        CredentialsRequest request, UserManager<AppUser> users, SignInManager<AppUser> signIn,
        SaveGuestRooms saveGuestRooms, HttpContext http, CancellationToken cancellationToken)
    {
        if (Missing(request) is { } missing)
        {
            return Problems.BadRequest(missing, Constants.Titles.SignInFailed);
        }

        var user = await users.FindByEmailAsync(request.Email!.Trim());

        if (user is null)
        {
            return Problems.BadRequest(Constants.Messages.WrongCredentials, Constants.Titles.SignInFailed);
        }

        var result = await signIn.PasswordSignInAsync(user, request.Password!, isPersistent: true, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return TypedResults.Problem(Constants.Messages.LockedOut,
                statusCode: StatusCodes.Status429TooManyRequests, title: Constants.Titles.SignInFailed);
        }

        if (!result.Succeeded)
        {
            return Problems.BadRequest(Constants.Messages.WrongCredentials, Constants.Titles.SignInFailed);
        }

        await SaveGuestRoomsAsync(saveGuestRooms, http, user, cancellationToken);

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
    /// <param name="saveGuestRooms">The use case.</param>
    /// <param name="http">The request.</param>
    /// <param name="user">The account just signed in to.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    private static async Task SaveGuestRoomsAsync(SaveGuestRooms saveGuestRooms, HttpContext http, AppUser user, CancellationToken cancellationToken)
    {
        if (GuestCookie.Read(http) is not { } guestId)
        {
            return;
        }

        await saveGuestRooms.HandleAsync(guestId, user.Id, cancellationToken);
        GuestCookie.Clear(http);
    }

    /// <summary>What's missing from the form, or <c>null</c> when both fields are filled in.</summary>
    /// <param name="request">The form.</param>
    private static string? Missing(CredentialsRequest request)
    {
        return string.IsNullOrWhiteSpace(request.Email) ? Constants.Messages.EnterEmail
        : string.IsNullOrEmpty(request.Password) ? Constants.Messages.EnterPassword
        : null;
    }

    /// <summary>Identity's errors in the app's words.</summary>
    /// <param name="errors">Why the account couldn't be created.</param>
    private static string Describe(IEnumerable<IdentityError> errors)
    {
        return string.Join(" ", errors
            .Select(e => e.Code switch
            {
                Constants.Accounts.ErrorCodes.DuplicateUserName or Constants.Accounts.ErrorCodes.DuplicateEmail => Constants.Messages.EmailTaken,
                Constants.Accounts.ErrorCodes.InvalidEmail or Constants.Accounts.ErrorCodes.InvalidUserName => Constants.Messages.InvalidEmail,
                Constants.Accounts.ErrorCodes.PasswordTooShort => Constants.Messages.PasswordTooShort,
                _ => e.Description,
            })
            .Distinct());
    }
}
