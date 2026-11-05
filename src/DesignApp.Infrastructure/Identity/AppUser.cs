using Microsoft.AspNetCore.Identity;

namespace DesignApp.Infrastructure.Identity;

/// <summary>
/// An account: ASP.NET Core Identity's user, signing in with email and password. Its id is what
/// rooms and designs store as their owner.
/// </summary>
public sealed class AppUser : IdentityUser;
