using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace DesignApp.Api;

/// <summary>Named rate-limit policies that endpoints opt into.</summary>
internal static class RateLimitPolicies
{
    /// <summary>Uploads lead to paid AI calls, so each client gets a limited number per hour.</summary>
    public const string Uploads = "uploads";

    /// <summary>Registers the rate limiter and its policies, limited per client IP address.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">Reads <c>RateLimits:UploadsPerHour</c> (default 30).</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddDesignAppRateLimits(this IServiceCollection services, IConfiguration configuration)
    {
        var uploadsPerHour = configuration.GetValue("RateLimits:UploadsPerHour", 30);

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Uploads, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = uploadsPerHour, Window = TimeSpan.FromHours(1) }));
        });
    }
}
