using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Infrastructure.Ai.Claude;
using Microsoft.Extensions.Logging;
using static DesignApp.Infrastructure.Constants;

using CC = DesignApp.Infrastructure.Constants.Ai.ClaudeCode;
using F = DesignApp.Infrastructure.Constants.Ai.ClaudeCode.Fields;
using G = DesignApp.Infrastructure.Constants.Ai.ClaudeCode.Flags;
using W = DesignApp.Infrastructure.Constants.Ai.ClaudeCode.Wire;

namespace DesignApp.Infrastructure.Ai.ClaudeCode;

/// <summary>
/// Development only: the same object listing as <see cref="ClaudeInventoryProvider"/> (same model, effort,
/// instructions, photo and JSON schema), sent through Claude Code on the developer's PC instead of the API, so
/// it runs on their Claude subscription. Claude Code gets only our system prompt and no tools.
/// </summary>
/// <param name="runner">Runs the <c>claude -p</c> process.</param>
/// <param name="settingsStore">Supplies the model, effort, limits and prompt, read per photo.</param>
/// <param name="logger">Records the tokens each call used (what the API would have charged).</param>
internal sealed partial class ClaudeCodeInventoryProvider(
    IClaudeCodeRunner runner,
    IAiSettingsStore settingsStore,
    ILogger<ClaudeCodeInventoryProvider> logger) : IObjectInventoryProvider
{
    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<InventoryItem>>> ListObjectsAsync(AnalysisImage image, CancellationToken cancellationToken)
    {
        var settings = await settingsStore.GetAsync(cancellationToken);
        var promptFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(promptFile, settings.SystemPrompt(), cancellationToken);
            var schema = JsonSerializer.Serialize(InventoryJson.Schema(settings.MaxPoints, image.Width, image.Height));
            var started = Stopwatch.GetTimestamp();
            var printed = await runner.RunAsync(Arguments(settings, promptFile, schema), Message(image, settings), cancellationToken);
            var elapsed = Stopwatch.GetElapsedTime(started);

            var result = Read(printed);
            // Claude Code caches the prompt itself; all of it counts as input, as on the uncached API route.
            LogUsage(result.Model, settings.InventoryEffort,
                result.InputTokens + result.CacheReadTokens + result.CacheWriteTokens, result.OutputTokens, elapsed.TotalSeconds);
            return InventoryJson.Parse(result.Answer, settings.MaxPoints, image.Width, image.Height);
        }
        finally
        {
            File.Delete(promptFile);
        }
    }

    /// <summary>
    /// The <c>claude -p</c> arguments: one stream-json message in, stream-json out (the last line is the result), our model, effort, system prompt and schema,
    /// no tools, no saved session, no MCP servers and no hooks.
    /// </summary>
    /// <param name="settings">The AI settings.</param>
    /// <param name="promptFile">The file holding the system prompt.</param>
    /// <param name="schema">The answer's JSON schema.</param>
    internal static IReadOnlyList<string> Arguments(AiSettings settings, string promptFile, string schema)
    {
        return
        [
            G.Print, G.InputFormat, G.StreamJson, G.OutputFormat, G.StreamJson, G.Verbose,
            G.Model, settings.InventoryModel,
            G.Effort, settings.InventoryEffort.ToString().ToLowerInvariant(),
            G.SystemPromptFile, promptFile,
            G.JsonSchema, schema,
            G.Tools, G.NoTools,
            G.MaxTurns, CC.MaxTurns.ToString(System.Globalization.CultureInfo.InvariantCulture),
            G.NoSessionPersistence,
            G.StrictMcpConfig,
            G.Settings, CC.HookFreeSettings,
        ];
    }

    /// <summary>The user message as one stream-json line: the photo, our user message and the photo's size.</summary>
    /// <param name="image">The analysis image.</param>
    /// <param name="settings">The AI settings.</param>
    internal static string Message(AnalysisImage image, AiSettings settings)
    {
        var message = new JsonObject
        {
            [W.Type] = W.User,
            [W.Message] = new JsonObject
            {
                [W.Role] = W.User,
                [W.Content] = new JsonArray(
                    new JsonObject
                    {
                        [W.Type] = W.Image,
                        [W.Source] = new JsonObject
                        {
                            [W.Type] = W.Base64,
                            [W.MediaType] = Application.Constants.ContentTypes.Jpeg,
                            [W.Data] = Convert.ToBase64String(image.Jpeg),
                        },
                    },
                    new JsonObject { [W.Type] = W.Text, [W.Text] = settings.UserMessage },
                    new JsonObject { [W.Type] = W.Text, [W.Text] = Constants.Ai.Claude.ImageSize(image.Width, image.Height) }),
            },
        };
        return message.ToJsonString();
    }

    /// <summary>Reads Claude Code's result line: the structured answer, and the model and tokens that produced it.</summary>
    /// <param name="printed">What Claude Code printed: stream-json lines, the last of type <c>result</c>.</param>
    /// <exception cref="InvalidOperationException">The run failed or gave no structured answer.</exception>
    internal static ClaudeCodeResult Read(string printed)
    {
        var line = printed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(IsResultLine)
            ?? throw new InvalidOperationException(CC.Messages.Failed(printed.Trim()));
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        var failed = root.TryGetProperty(F.IsError, out var isError) && isError.GetBoolean();
        if (failed || !root.TryGetProperty(F.StructuredOutput, out var answer) || answer.ValueKind != JsonValueKind.Object)
        {
            var reason = root.TryGetProperty(F.Result, out var text) ? text.ToString() : printed;
            throw new InvalidOperationException(CC.Messages.Failed(reason));
        }

        // The model that did the work is the one with the most output (Claude Code may use another for side tasks).
        string model = "";
        long input = 0, output = -1, cacheRead = 0, cacheWrite = 0;
        if (root.TryGetProperty(F.ModelUsage, out var usage) && usage.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in usage.EnumerateObject())
            {
                var outTokens = Tokens(entry.Value, F.OutputTokens);
                if (outTokens <= output)
                    continue;
                (model, output) = (entry.Name, outTokens);
                input = Tokens(entry.Value, F.InputTokens);
                cacheRead = Tokens(entry.Value, F.CacheReadTokens);
                cacheWrite = Tokens(entry.Value, F.CacheWriteTokens);
            }
        }
        var cost = root.TryGetProperty(F.CostUsd, out var c) && c.ValueKind == JsonValueKind.Number ? c.GetDouble() : 0;
        return new ClaudeCodeResult(answer.GetRawText(), model, input, Math.Max(output, 0), cacheRead, cacheWrite, cost);
    }

    /// <summary>Whether a printed line is Claude Code's result (a JSON object whose type is <c>result</c>).</summary>
    /// <param name="line">One printed line.</param>
    private static bool IsResultLine(string line)
    {
        if (!line.StartsWith('{'))
            return false;
        try
        {
            using var document = JsonDocument.Parse(line);
            return document.RootElement.TryGetProperty(F.Type, out var type) && type.GetString() == F.ResultType;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>A token count from a model-usage entry, 0 when absent.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="name">The field.</param>
    private static long Tokens(JsonElement entry, string name)
    {
        return entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt64() : 0;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Claude {Model} via Claude Code ({Effort} effort) listed the objects with {InputTokens} input and {OutputTokens} output tokens in {Seconds:0.0} s")]
    private partial void LogUsage(string model, InventoryEffort effort, long inputTokens, long outputTokens, double seconds);
}
