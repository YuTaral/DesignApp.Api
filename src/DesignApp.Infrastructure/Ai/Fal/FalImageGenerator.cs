using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using Microsoft.Extensions.Logging;
using Polly.Timeout;

using IM = DesignApp.Infrastructure.Constants.Ai.Images;
using J = DesignApp.Infrastructure.Constants.Ai.Images.Json;

namespace DesignApp.Infrastructure.Ai.Fal;

/// <summary>
/// Turns a built room's 3D view into a realistic photo, with the objects' decorations, with FLUX 3 Image Klein on
/// fal.ai (the model is <see cref="AiSettings.ImageEndpoint"/>): the image and the instruction are queued as one paid
/// job, then the job is followed until the photo is ready. A slow answer never sends it again.
/// </summary>
/// <param name="http">The fal.ai client (key, timeout per call, no retries).</param>
/// <param name="settingsStore">The AI settings, read per photo.</param>
/// <param name="logger">Records how long each photo took, and failures.</param>
internal sealed partial class FalImageGenerator(HttpClient http, IAiSettingsStore settingsStore, ILogger<FalImageGenerator> logger) : IImageGenerator
{
    /// <inheritdoc/>
    public async Task<Result<byte[]>> GenerateAsync(byte[] image, string prompt, int? seed, IReadOnlyList<LayoutBox>? boxes, CancellationToken cancellationToken)
    {
        var settings = await settingsStore.GetAsync(cancellationToken);
        var request = new ImageRequest(prompt, [DataUri(image)], new ImageSize(IM.Width, IM.Height), IM.OutputFormat, IM.PromptExpansion, seed,
            boxes?.Select(b => new EditBox(b.Label, b.Description, new Box(b.Left, b.Top, b.Right, b.Bottom))).ToList());
        var started = Stopwatch.GetTimestamp();

        try
        {
            using var submitted = await http.PostAsJsonAsync(new Uri(settings.ImageEndpoint), request, cancellationToken);

            submitted.EnsureSuccessStatusCode();
            var ticket = await submitted.Content.ReadFromJsonAsync<QueueTicket>(cancellationToken);

            if (ticket is not { StatusUrl.Length: > 0, ResponseUrl.Length: > 0 })
            {
                return Failed("the queue gave no status or result address");
            }

            var status = await WaitAsync(ticket.StatusUrl, started, cancellationToken);

            if (status != IM.Completed)
            {
                return Failed(status);
            }

            var answer = await http.GetFromJsonAsync<ImageResponse>(new Uri(ticket.ResponseUrl), cancellationToken);

            if (answer?.Images is not [{ Url: { Length: > 0 } url }, ..])
            {
                return Failed("no image in the result");
            }

            var photo = url.StartsWith(Constants.Ai.Fal.DataUriScheme, StringComparison.Ordinal)
                ? Convert.FromBase64String(url[(url.IndexOf(',', StringComparison.Ordinal) + 1)..])
                : await http.GetByteArrayAsync(new Uri(url), cancellationToken);
            var seconds = Stopwatch.GetElapsedTime(started).TotalSeconds;

            LogGenerated(settings.ImageEndpoint, seconds);

            return photo;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or FormatException or TimeoutRejectedException
            || (e is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return Failed(e.Message);
        }
    }

    /// <summary>An image inline, as a data URI of its own type: the 3D view is a PNG, a photo to decorate a JPEG.</summary>
    /// <param name="image">The image.</param>
    private static string DataUri(byte[] image)
    {
        var jpeg = image is [0xFF, 0xD8, ..];

        return (jpeg ? IM.JpegDataUri : IM.PngDataUri) + Convert.ToBase64String(image);
    }

    /// <summary>Follows the queued job until it is done or given up.</summary>
    /// <param name="statusUrl">Where its status is.</param>
    /// <param name="started">When it was submitted.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns><see cref="IM.Completed"/>, or why it failed.</returns>
    private async Task<string> WaitAsync(string statusUrl, long started, CancellationToken cancellationToken)
    {
        var errors = 0;

        while (Stopwatch.GetElapsedTime(started) < IM.MaxWait)
        {
            await Task.Delay(IM.PollInterval, cancellationToken);

            try
            {
                var status = await http.GetFromJsonAsync<QueueStatus>(new Uri(statusUrl), cancellationToken);

                errors = 0;

                if (status?.Status != IM.Completed)
                {
                    continue;
                }

                return status.Error is { Length: > 0 } error ? error : IM.Completed;
            }
            catch (Exception e) when (e is HttpRequestException or JsonException or TimeoutRejectedException)
            {
                // A status check is free, so a failed one is simply tried again.
                if (++errors >= IM.MaxStatusErrors)
                {
                    return e.Message;
                }
            }
        }

        return $"not done after {IM.MaxWait.TotalMinutes:0} minutes";
    }

    /// <summary>Logs why a photo failed and gives the user's message.</summary>
    /// <param name="reason">The reason, for the log.</param>
    private Result<byte[]> Failed(string reason)
    {
        LogFailed(reason);

        return Result.Failure<byte[]>(IM.Messages.Failed);
    }

    /// <summary>The job sent to the queue.</summary>
    /// <param name="Prompt">The instruction.</param>
    /// <param name="ImageUrls">The image to start from (the 3D view, or the photo to decorate), inline.</param>
    /// <param name="ImageSize">The photo's size.</param>
    /// <param name="OutputFormat">The photo's format.</param>
    /// <param name="PromptExpansion">Whether the model may rewrite the prompt first.</param>
    /// <param name="Seed">Makes the photo repeatable; left out (a random photo) when null.</param>
    /// <param name="BoundingBoxes">Objects to draw inside given boxes; left out when null.</param>
    private sealed record ImageRequest(
        [property: JsonPropertyName(J.Prompt)] string Prompt,
        [property: JsonPropertyName(J.ImageUrls)] IReadOnlyList<string> ImageUrls,
        [property: JsonPropertyName(J.ImageSize)] ImageSize ImageSize,
        [property: JsonPropertyName(J.OutputFormat)] string OutputFormat,
        [property: JsonPropertyName(J.PromptExpansion)] bool PromptExpansion,
        [property: JsonPropertyName(J.Seed), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Seed,
        [property: JsonPropertyName(J.BoundingBoxes), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<EditBox>? BoundingBoxes);

    /// <summary>An object the model draws inside a box of the photo.</summary>
    /// <param name="Label">Its id.</param>
    /// <param name="Description">What it is and how it looks.</param>
    /// <param name="Box">Where it goes.</param>
    private sealed record EditBox(
        [property: JsonPropertyName(J.Label)] string Label,
        [property: JsonPropertyName(J.Description)] string Description,
        [property: JsonPropertyName(J.Box)] Box Box);

    /// <summary>A box as shares of the photo, 0 at the left and top.</summary>
    /// <param name="XMin">Its left edge.</param>
    /// <param name="YMin">Its top edge.</param>
    /// <param name="XMax">Its right edge.</param>
    /// <param name="YMax">Its bottom edge.</param>
    private sealed record Box(
        [property: JsonPropertyName(J.XMin)] double XMin,
        [property: JsonPropertyName(J.YMin)] double YMin,
        [property: JsonPropertyName(J.XMax)] double XMax,
        [property: JsonPropertyName(J.YMax)] double YMax);

    /// <summary>A photo's size in pixels.</summary>
    /// <param name="Width">Its width.</param>
    /// <param name="Height">Its height.</param>
    private sealed record ImageSize(
        [property: JsonPropertyName(J.Width)] int Width,
        [property: JsonPropertyName(J.Height)] int Height);

    /// <summary>Where the queued job can be followed.</summary>
    /// <param name="StatusUrl">Its status.</param>
    /// <param name="ResponseUrl">Its result.</param>
    private sealed record QueueTicket(
        [property: JsonPropertyName(J.StatusUrl)] string? StatusUrl,
        [property: JsonPropertyName(J.ResponseUrl)] string? ResponseUrl);

    /// <summary>The job's status.</summary>
    /// <param name="Status">Queued, in progress or completed.</param>
    /// <param name="Error">Why it failed, if it did.</param>
    private sealed record QueueStatus(
        [property: JsonPropertyName(J.Status)] string? Status,
        [property: JsonPropertyName(J.Error)] string? Error);

    /// <summary>The job's result.</summary>
    /// <param name="Images">The photos.</param>
    private sealed record ImageResponse([property: JsonPropertyName(J.Images)] List<ResultImage>? Images);

    /// <summary>One photo of the result.</summary>
    /// <param name="Url">Where it is, or the photo inline.</param>
    private sealed record ResultImage([property: JsonPropertyName(J.Url)] string? Url);

    [LoggerMessage(Level = LogLevel.Information, Message = "fal.ai ({Endpoint}) made a photo of a built room in {Seconds:0.0} s")]
    private partial void LogGenerated(string endpoint, double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "fal.ai couldn't make a photo of a built room: {Reason}")]
    private partial void LogFailed(string reason);
}
