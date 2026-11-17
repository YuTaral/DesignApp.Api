using DesignApp.Api.Services;
using DesignApp.Application.Interfaces;
using DesignApp.Infrastructure.Database;
using DesignApp.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace DesignApp.Api.Setup;

/// <summary>Wires up accounts: ASP.NET Core Identity with email and password, kept in a cookie.</summary>
internal static class AccountSetup
{
    /// <summary>Adds Identity, the sign-in cookie and <see cref="ICurrentUser"/>.</summary>
    /// <param name="services">The app's services.</param>
    /// <returns>The same services, for chaining.</returns>
    public static IServiceCollection AddDesignAppAccounts(this IServiceCollection services)
    {
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.AddAuthorization();
        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = Constants.Accounts.MinPasswordLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = Constants.Accounts.MaxFailedAttempts;
                options.Lockout.DefaultLockoutTimeSpan = Constants.Accounts.LockoutTime;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = Constants.Cookies.Session;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            // Lax: other sites can't send it with a POST, so they can't act as the user.
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = Constants.Cookies.SessionLifetime;
            options.SlidingExpiration = true;
            // An API answers with a status, not a redirect to a sign-in page.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;

                return Task.CompletedTask;
            };
        });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        return services;
    }
}
