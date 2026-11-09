namespace DesignApp.Api.Accounts;

/// <summary>Body of <c>POST /api/account/sign-up</c> and <c>/sign-in</c>.</summary>
/// <param name="Email">The account's email.</param>
/// <param name="Password">The password.</param>
internal sealed record CredentialsRequest(string? Email, string? Password);
