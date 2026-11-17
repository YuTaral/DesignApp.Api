using System.Threading.RateLimiting;

namespace DesignApp.Api.Setup;

/// <summary>Named rate-limit policies that endpoints opt into.</summary>
internal static class RateLimitPolicies
{
    /// <summary>Registers the rate limiter and its policies, limited per client IP address.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">
    /// Reads <c>RateLimits:UploadsPerHour</c> (default 30), <c>RateLimits:AccountAttemptsPer10Minutes</c> (default 20),
    /// <c>RateLimits:RemovalsPerHour</c> (default 30) and <c>RateLimits:Enabled</c> (default true; when false, as in
    /// development, nothing is limited).
    /// </param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddDesignAppRateLimits(this IServiceCollection services, IConfiguration configuration)
    {
        var uploadsPerHour = configuration.GetValue(Constants.RateLimits.UploadsPerHourKey, Constants.RateLimits.DefaultUploadsPerHour);
        var accountAttempts = configuration.GetValue(Constants.RateLimits.AccountAttemptsKey, Constants.RateLimits.DefaultAccountAttempts);
        var removalsPerHour = configuration.GetValue(Constants.RateLimits.RemovalsPerHourKey, Constants.RateLimits.DefaultRemovalsPerHour);
        var enabled = configuration.GetValue(Constants.RateLimits.EnabledKey, true);

        // One limiter per client address, or none when the limits are off.
        RateLimitPartition<string> PerAddress(HttpContext context, int permits, TimeSpan window)
        {
            var address = context.Connection.RemoteIpAddress?.ToString() ?? Constants.RateLimits.UnknownAddress;

            if (!enabled)
            {
                return RateLimitPartition.GetNoLimiter(address);
            }

            return RateLimitPartition.GetFixedWindowLimiter(address, _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = window });
        }

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Constants.RateLimits.UploadsPolicy, context => PerAddress(context, uploadsPerHour, Constants.RateLimits.UploadsWindow));
            options.AddPolicy(Constants.RateLimits.AccountsPolicy, context => PerAddress(context, accountAttempts, Constants.RateLimits.AccountsWindow));
            options.AddPolicy(Constants.RateLimits.RemovalsPolicy, context => PerAddress(context, removalsPerHour, Constants.RateLimits.RemovalsWindow));
        });
    }
}
