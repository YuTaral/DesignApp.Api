using System.Text.Json;
using Polly.Timeout;

namespace DesignApp.Infrastructure.Ai.Fal;

/// <summary>What every call to fal.ai shares: reading the file an answer points at, and which errors mean a failed call.</summary>
internal static class FalCalls
{
    /// <summary>The file an answer points at: inline, as a data URI, or at an address to download.</summary>
    /// <param name="http">The fal.ai client.</param>
    /// <param name="url">The data URI or address.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The file's bytes.</returns>
    public static async Task<byte[]> ReadFileAsync(this HttpClient http, string url, CancellationToken cancellationToken)
    {
        if (url.StartsWith(Constants.Ai.Fal.DataUriScheme, StringComparison.Ordinal))
        {
            return Convert.FromBase64String(url[(url.IndexOf(',', StringComparison.Ordinal) + 1)..]);
        }

        return await http.GetByteArrayAsync(new Uri(url), cancellationToken);
    }

    /// <summary>
    /// Whether an error means the call failed (the service, its answer or the time it took), so the user is told,
    /// rather than a bug or the user cancelling.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="cancellationToken">The call's token: a cancel through it is the user's, not a failure.</param>
    public static bool IsCallFailure(Exception error, CancellationToken cancellationToken)
    {
        return error is HttpRequestException or JsonException or FormatException or TimeoutRejectedException
            || (error is TaskCanceledException && !cancellationToken.IsCancellationRequested);
    }
}
