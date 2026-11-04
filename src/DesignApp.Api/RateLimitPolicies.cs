using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace DesignApp.Api;

internal static class RateLimitPolicies
{
    /// <summary>Uploads lead to paid AI calls, so each client gets a limited number per hour.</summary>
    public const string Uploads = "uploads";

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
