using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace DesignApp.Api;

/// <summary>Named rate-limit policies that endpoints opt into.</summary>
internal static class RateLimitPolicies
{
    /// <summary>Registers the rate limiter and its policies, limited per client IP address.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">
    /// Reads <c>RateLimits:UploadsPerHour</c> (default 30) and <c>RateLimits:AccountAttemptsPer10Minutes</c> (default 20).
    /// </param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddDesignAppRateLimits(this IServiceCollection services, IConfiguration configuration)
    {
        var uploadsPerHour = configuration.GetValue(Constants.RateLimits.UploadsPerHourKey, Constants.RateLimits.DefaultUploadsPerHour);
        var accountAttempts = configuration.GetValue(Constants.RateLimits.AccountAttemptsKey, Constants.RateLimits.DefaultAccountAttempts);

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Constants.RateLimits.UploadsPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? Constants.RateLimits.UnknownAddress,
                _ => new FixedWindowRateLimiterOptions { PermitLimit = uploadsPerHour, Window = Constants.RateLimits.UploadsWindow }));
            options.AddPolicy(Constants.RateLimits.AccountsPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? Constants.RateLimits.UnknownAddress,
                _ => new FixedWindowRateLimiterOptions { PermitLimit = accountAttempts, Window = Constants.RateLimits.AccountsWindow }));
        });
    }
}
