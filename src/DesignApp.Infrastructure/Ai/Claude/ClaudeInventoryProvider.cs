using System.Diagnostics;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Objects;
using DesignApp.Infrastructure.Ai.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static DesignApp.Infrastructure.Constants;

namespace DesignApp.Infrastructure.Ai.Claude;

/// <summary>
/// Step 1 of the analysis on Claude (Sonnet 5.5 by default): one vision request with structured
/// output, so the answer always matches <see cref="InventoryJson.Schema"/>.
/// </summary>
/// <param name="client">The Anthropic client (it retries rate limits and server errors itself).</param>
/// <param name="options">Says whether to record the usage of sample photos.</param>
/// <param name="settingsStore">Supplies the model, effort, limits and prompt, read per photo.</param>
/// <param name="logger">Records the tokens each call used, which is what it costs.</param>
internal sealed partial class ClaudeInventoryProvider(
    IAnthropicClient client,
    IOptions<AiOptions> options,
    IAiSettingsStore settingsStore,
    ILogger<ClaudeInventoryProvider> logger) : IObjectInventoryProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<InventoryItem>>> ListObjectsAsync(AnalysisImage image, CancellationToken cancellationToken)
    {
        var settings = await settingsStore.GetAsync(cancellationToken);
        var started = Stopwatch.GetTimestamp();
        var response = await AskAsync(image, settings, settings.SystemPrompt(), settings.UserMessage, cancellationToken);

        var elapsed = Stopwatch.GetElapsedTime(started);

        LogUsage(settings.InventoryModel, settings.InventoryEffort, response.Usage.InputTokens, response.Usage.OutputTokens, elapsed.TotalSeconds);
        await RecordUsageAsync(image, options.Value.RecordSamplesTo, settings, response.Usage, elapsed, cancellationToken);

        return Read(response, settings, image);
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<InventoryItem>>> ListPartsAsync(AnalysisImage image, DetectedObject whole, string? instructions, CancellationToken cancellationToken)
    {
        var settings = await settingsStore.GetAsync(cancellationToken);
        var started = Stopwatch.GetTimestamp();
        var box = whole.Box;
        var message = Constants.Ai.Claude.PartsMessage(whole.Label,
            (int)(box.X * image.Width), (int)(box.Y * image.Height), (int)(box.Width * image.Width), (int)(box.Height * image.Height), instructions);
        var response = await AskAsync(image, settings, Constants.Ai.Claude.PartsSystemPrompt, message, cancellationToken);
        var seconds = Stopwatch.GetElapsedTime(started).TotalSeconds;

        LogPartsUsage(settings.InventoryModel, response.Usage.InputTokens, response.Usage.OutputTokens, seconds);
        var parts = Read(response, settings, image);

        if (parts.Succeeded)
        {
            var names = string.Join(", ", parts.Value.Select(p => p.Label));

            LogParts(whole.Label, names);
        }

        return parts;
    }

    /// <summary>Sends the photo with instructions and a message, asking for an answer in the inventory's JSON schema.</summary>
    /// <param name="image">The photo.</param>
    /// <param name="settings">The model, effort and limits.</param>
    /// <param name="system">The instructions.</param>
    /// <param name="text">The message sent with the photo.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    private async Task<Message> AskAsync(AnalysisImage image, AiSettings settings, string system, string text, CancellationToken cancellationToken)
    {
        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model = settings.InventoryModel,
            MaxTokens = settings.MaxTokens,
            System = system,
            OutputConfig = new OutputConfig
            {
                Effort = settings.InventoryEffort switch
                {
                    InventoryEffort.Low => Effort.Low,
                    InventoryEffort.High => Effort.High,
                    _ => Effort.Medium,
                },
                Format = new JsonOutputFormat { Schema = InventoryJson.Schema(settings.MaxPoints, image.Width, image.Height) },
            },
            Messages =
            [
                new UserMessageParam
                {
                    Content = new List<ContentBlockParam>
                    {
                        new ImageBlockParam
                        {
                            Source = new Base64ImageSource { Data = Convert.ToBase64String(image.Jpeg), MediaType = MediaType.ImageJpeg },
                        },
                        new TextBlockParam { Text = text },
                        new TextBlockParam { Text = Constants.Ai.Claude.ImageSize(image.Width, image.Height) },
                    },
                },
            ],
        }, cancellationToken);

        return response;
    }

    /// <summary>Reads Claude's answer.</summary>
    /// <param name="response">The answer.</param>
    /// <param name="settings">The limits the answer is kept to.</param>
    /// <param name="image">The photo the boxes are measured in.</param>
    /// <exception cref="InvalidOperationException">Claude stopped before finishing.</exception>
    private static Result<IReadOnlyList<InventoryItem>> Read(Message response, AiSettings settings, AnalysisImage image)
    {
        if (response.StopReason != StopReason.EndTurn)
        {
            throw new InvalidOperationException($"Claude stopped early ({response.StopReason}) while listing objects.");
        }

        var json = ClaudeAnswer.Text(response);

        return InventoryJson.Parse(json, settings.MaxPoints, image.Width, image.Height);
    }

    /// <summary>
    /// When a sample photo is analyzed with <see cref="AiOptions.RecordSamplesTo"/> set, saves the
    /// call's token counts next to its recording as <c>name.claude-usage.json</c>, so models and
    /// effort levels can be compared on the same photos. Development use only.
    /// </summary>
    /// <param name="image">The analyzed photo.</param>
    /// <param name="recordTo">The folder to record into, if recording is on.</param>
    /// <param name="settings">The AI settings the call used.</param>
    /// <param name="usage">The tokens Claude reported.</param>
    /// <param name="elapsed">How long the call took.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    private static async Task RecordUsageAsync(
        AnalysisImage image, string? recordTo, AiSettings settings, Usage usage, TimeSpan elapsed, CancellationToken cancellationToken)
    {
        if (recordTo is not { Length: > 0 } folder || SampleRooms.Match(image) is not { } sample)
        {
            return;
        }

        var recorded = new
        {
            name = sample.Name,
            model = settings.InventoryModel,
            effort = settings.InventoryEffort.ToString(),
            inputTokens = usage.InputTokens,
            outputTokens = usage.OutputTokens,
            seconds = Math.Round(elapsed.TotalSeconds, 1),
        };

        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, sample.Name + Constants.Samples.ClaudeUsageSuffix), JsonSerializer.Serialize(recorded, Json), cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Claude {Model} ({Effort} effort) listed the objects with {InputTokens} input and {OutputTokens} output tokens in {Seconds:0.0} s")]
    private partial void LogUsage(string model, InventoryEffort effort, long inputTokens, long outputTokens, double seconds);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Claude {Model} listed an object's parts with {InputTokens} input and {OutputTokens} output tokens in {Seconds:0.0} s")]
    private partial void LogPartsUsage(string model, long inputTokens, long outputTokens, double seconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "Claude listed the parts of the {Object}: {Parts}")]
    private partial void LogParts(string @object, string parts);
}
