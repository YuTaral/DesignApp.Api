using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Infrastructure.Photos;
using Microsoft.Extensions.Logging;
using Polly.Timeout;
using static DesignApp.Infrastructure.Constants;

namespace DesignApp.Infrastructure.Ai.Fal;

/// <summary>
/// An object eraser on fal.ai (Bria's by default; fal's own or Ideogram's also work), through fal.ai's queue: the photo and a mask are
/// submitted once (each submit is a paid job, so it is never repeated), then the job's status is checked until it is
/// done, and its result fetched. A slow job takes longer instead of failing, up to
/// <see cref="Constants.Ai.Removal.MaxWait"/>. The endpoint, quality tier (its price) and mask growth come from the
/// AI settings, read per call.
/// </summary>
/// <param name="http">An HTTP client with the fal.ai key and a timeout per call, without retries.</param>
/// <param name="settingsStore">The AI settings.</param>
/// <param name="logger">Records each removal and its tier, so its cost can be counted.</param>
internal sealed partial class FalObjectRemovalProvider(HttpClient http, IAiSettingsStore settingsStore, ILogger<FalObjectRemovalProvider> logger)
    : IObjectRemovalProvider
{
    /// <inheritdoc/>
    public async Task<Result<byte[]>> RemoveAsync(byte[] photoJpeg, byte[] maskPng, bool brushed, CancellationToken cancellationToken)
    {
        var settings = await settingsStore.GetAsync(cancellationToken);
        // A brushed mark hugs the object's outline; grown, the hole no longer has the object's shape.
        var mask = brushed ? MaskGrowth.Grow(maskPng, settings.RemovalHandMaskGrowth) : maskPng;
        var request = new RemovalRequest(
            Constants.Ai.Fal.JpegDataUri + Convert.ToBase64String(photoJpeg),
            Constants.Ai.Removal.PngDataUri + Convert.ToBase64String(mask),
            Constants.Ai.Removal.WireQuality(settings.RemovalQuality),
            settings.RemovalMaskExpansion,
            brushed ? Constants.Ai.Removal.ManualMask : Constants.Ai.Removal.AutomaticMask);
        var started = Stopwatch.GetTimestamp();

        try
        {
            var ticket = await SubmitAsync(settings, request, cancellationToken);

            if (ticket is null)
            {
                return Failed("the queue gave no status or result address");
            }

            var status = await WaitAsync(ticket.StatusUrl!, started, cancellationToken);

            if (status != Constants.Ai.Removal.Completed)
            {
                return Failed(status);
            }

            var answer = await http.GetFromJsonAsync<RemovalResponse>(new Uri(ticket.ResponseUrl!), cancellationToken);
            // fal's own eraser answers with a list of images, Bria's and Ideogram's with one image.
            var url = answer?.Images is [{ Url: { Length: > 0 } first }, ..] ? first : answer?.Image?.Url;

            if (string.IsNullOrEmpty(url))
            {
                return Failed("no image in the result");
            }

            var image = url.StartsWith(Constants.Ai.Fal.DataUriScheme, StringComparison.Ordinal)
                ? Convert.FromBase64String(url[(url.IndexOf(',', StringComparison.Ordinal) + 1)..])
                : await http.GetByteArrayAsync(new Uri(url), cancellationToken);
            var seconds = Stopwatch.GetElapsedTime(started).TotalSeconds;

            LogRemoved(settings.RemovalQuality, seconds);

            return image;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or FormatException or TimeoutRejectedException
            || (e is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return Failed(e.Message);
        }
    }

    /// <summary>Submits the job to the queue, once.</summary>
    /// <param name="settings">The AI settings.</param>
    /// <param name="request">The job.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Where to follow the job, or <c>null</c> when the queue didn't say.</returns>
    private async Task<QueueTicket?> SubmitAsync(AiSettings settings, RemovalRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(new Uri(settings.RemovalEndpoint), request, cancellationToken);

        response.EnsureSuccessStatusCode();
        var ticket = await response.Content.ReadFromJsonAsync<QueueTicket>(cancellationToken);

        return ticket is { StatusUrl.Length: > 0, ResponseUrl.Length: > 0 } ? ticket : null;
    }

    /// <summary>Checks the job's status until it is done, failed for good, or out of time.</summary>
    /// <param name="statusUrl">Where to check.</param>
    /// <param name="started">When the removal started.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns><see cref="Constants.Ai.Removal.Completed"/>, or why it isn't.</returns>
    private async Task<string> WaitAsync(string statusUrl, long started, CancellationToken cancellationToken)
    {
        var errors = 0;

        while (Stopwatch.GetElapsedTime(started) < Constants.Ai.Removal.MaxWait)
        {
            await Task.Delay(Constants.Ai.Removal.PollInterval, cancellationToken);

            try
            {
                var status = await http.GetFromJsonAsync<QueueStatus>(new Uri(statusUrl), cancellationToken);

                errors = 0;

                if (status?.Status != Constants.Ai.Removal.Completed)
                {
                    continue;
                }

                return status.Error is { Length: > 0 } error ? error : Constants.Ai.Removal.Completed;
            }
            catch (Exception e) when (e is HttpRequestException or JsonException or TimeoutRejectedException)
            {
                // A status check is free, so a failed one is simply tried again.
                if (++errors >= Constants.Ai.Removal.MaxStatusErrors)
                {
                    return e.Message;
                }
            }
        }

        return $"not done after {Constants.Ai.Removal.MaxWait.TotalMinutes:0} minutes";
    }

    /// <summary>Logs why a removal failed and returns the user's message.</summary>
    /// <param name="reason">Why it failed.</param>
    private Result<byte[]> Failed(string reason)
    {
        LogFailed(reason);

        return Result.Failure<byte[]>(Constants.Ai.Removal.Messages.Failed);
    }

    /// <summary>The eraser's request.</summary>
    /// <param name="ImageUrl">The photo, as a data URI.</param>
    /// <param name="MaskUrl">The mask, as a data URI (white = remove).</param>
    /// <param name="Model">The quality tier's wire name.</param>
    /// <param name="MaskExpansion">How many pixels to grow the mask by.</param>
    /// <param name="MaskType">What made the mask: SAM 3 (automatic) or the user's brush (manual); Bria's eraser treats them differently.</param>
    private sealed record RemovalRequest(
        [property: JsonPropertyName(Constants.Ai.Removal.Json.ImageUrl)] string ImageUrl,
        [property: JsonPropertyName(Constants.Ai.Removal.Json.MaskUrl)] string MaskUrl,
        [property: JsonPropertyName(Constants.Ai.Removal.Json.Model)] string Model,
        [property: JsonPropertyName(Constants.Ai.Removal.Json.MaskExpansion)] int MaskExpansion,
        [property: JsonPropertyName(Constants.Ai.Removal.Json.MaskType)] string MaskType);

    /// <summary>The queue's answer to a submit: where to follow the job.</summary>
    /// <param name="StatusUrl">Where to check its status.</param>
    /// <param name="ResponseUrl">Where to fetch its result.</param>
    private sealed record QueueTicket(
        [property: JsonPropertyName(Constants.Ai.Removal.Json.StatusUrl)] string? StatusUrl,
        [property: JsonPropertyName(Constants.Ai.Removal.Json.ResponseUrl)] string? ResponseUrl);

    /// <summary>A queued job's status.</summary>
    /// <param name="Status">IN_QUEUE, IN_PROGRESS or COMPLETED.</param>
    /// <param name="Error">Why it failed, when it did.</param>
    private sealed record QueueStatus(
        [property: JsonPropertyName(Constants.Ai.Removal.Json.Status)] string? Status,
        [property: JsonPropertyName(Constants.Ai.Removal.Json.Error)] string? Error);

    /// <summary>The eraser's result: a list of images or a single image, by eraser.</summary>
    /// <param name="Images">The result images; the first is the photo.</param>
    /// <param name="Image">The result image.</param>
    private sealed record RemovalResponse(
        [property: JsonPropertyName(Constants.Ai.Removal.Json.Images)] List<RemovalImage>? Images,
        [property: JsonPropertyName(Constants.Ai.Removal.Json.Image)] RemovalImage? Image);

    /// <summary>One result image.</summary>
    /// <param name="Url">Where to get it, or the image itself as a data URI.</param>
    private sealed record RemovalImage([property: JsonPropertyName(Constants.Ai.Removal.Json.Url)] string? Url);

    [LoggerMessage(Level = LogLevel.Information, Message = "fal.ai removed an object from a photo ({Quality} quality) in {Seconds:0.0} s")]
    private partial void LogRemoved(RemovalQuality quality, double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "fal.ai couldn't remove an object: {Reason}")]
    private partial void LogFailed(string reason);
}
