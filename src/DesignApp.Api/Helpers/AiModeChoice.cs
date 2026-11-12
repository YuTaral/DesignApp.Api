using DesignApp.Application.Objects;
using Microsoft.AspNetCore.Http.HttpResults;

using static DesignApp.Domain.Constants;

namespace DesignApp.Api.Helpers;

/// <summary>Reads the AI a request asks for (an upload's <c>ai</c> field, a library copy's <c>ai</c> query) and checks that it may be used.</summary>
internal static class AiModeChoice
{
    /// <summary>The AI to use: the requested one if allowed, the default when none is asked for.</summary>
    /// <param name="ai">The requested AI, <c>Fake</c> or <c>Real</c>; empty for the default.</param>
    /// <param name="availability">Which AI the API offers.</param>
    /// <param name="mode">The AI to use, when there is no problem.</param>
    /// <returns><c>null</c>, or the 400 problem for an unknown or unavailable AI.</returns>
    public static ProblemHttpResult? TryResolve(string? ai, AiAvailability availability, out AiMode mode)
    {
        mode = availability.Default;
        if (!string.IsNullOrWhiteSpace(ai) && (!Enum.TryParse(ai, ignoreCase: true, out mode) || !Enum.IsDefined(mode)))
            return TypedResults.Problem(Constants.Messages.UnknownAi(ai), statusCode: StatusCodes.Status400BadRequest, title: Constants.Titles.InvalidAi);
        if (availability.Allows(mode))
            return null;

        var why = mode is AiMode.Real && !availability.RealAvailable
            ? Constants.Messages.RealAiNotSetUp
            : Constants.Messages.OnlyAi(availability.Default.ToString());
        return TypedResults.Problem(why, statusCode: StatusCodes.Status400BadRequest, title: Constants.Titles.AiNotAvailable);
    }
}
