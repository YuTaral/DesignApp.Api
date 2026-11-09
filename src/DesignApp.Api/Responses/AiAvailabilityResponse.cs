using DesignApp.Application.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Api.Responses;

/// <summary>The body of <c>GET /api/ai</c>: which AI uploads use, and whether they may choose.</summary>
/// <param name="Default">The AI an upload gets when it doesn't choose.</param>
/// <param name="CanChoose">Whether the upload page shows the fake/real switch (Development only).</param>
/// <param name="RealAvailable">Whether the real AI's keys are set.</param>
public sealed record AiAvailabilityResponse(AiMode Default, bool CanChoose, bool RealAvailable)
{
    /// <summary>Maps the availability to its API shape.</summary>
    /// <param name="availability">What the API can use.</param>
    public static AiAvailabilityResponse From(AiAvailability availability) =>
        new(availability.Default, availability.CanChoose, availability.RealAvailable);
}
