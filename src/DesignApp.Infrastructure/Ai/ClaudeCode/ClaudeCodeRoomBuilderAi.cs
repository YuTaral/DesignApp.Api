using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Infrastructure.Ai.Claude;
using Microsoft.Extensions.Logging;

using static DesignApp.Domain.Constants;
using CC = DesignApp.Infrastructure.Constants.Ai.ClaudeCode;
using G = DesignApp.Infrastructure.Constants.Ai.ClaudeCode.Flags;
using RB = DesignApp.Infrastructure.Constants.Ai.RoomBuilder;

namespace DesignApp.Infrastructure.Ai.ClaudeCode;

/// <summary>
/// The room builder's language model on Claude Haiku through Claude Code (<c>claude -p</c>) on the developer's Claude
/// subscription, in Development only, like <see cref="ClaudeCodeInventoryProvider"/>: the same instructions and object
/// schema as <see cref="ClaudeRoomBuilderAi"/>.
/// </summary>
/// <param name="runner">Runs the <c>claude -p</c> process.</param>
/// <param name="logger">Records the tokens each call used.</param>
internal sealed partial class ClaudeCodeRoomBuilderAi(IClaudeCodeRunner runner, ILogger<ClaudeCodeRoomBuilderAi> logger) : IRoomBuilderAi
{
    /// <inheritdoc/>
    public async Task<Result<CatalogObject>> DescribeObjectAsync(RoomType roomType, string description, CancellationToken cancellationToken)
    {
        var answer = await AskAsync(RB.DescribeSystem, RB.DescribeMessage(ClaudeRoomBuilderAi.Words(roomType.ToString()), description),
            ClaudeRoomBuilderAi.Schema(), "described an object", cancellationToken);

        return ClaudeRoomBuilderAi.ReadObject(answer);
    }

    /// <summary>Runs Claude Code once with instructions, a text and an answer schema.</summary>
    /// <param name="system">The instructions.</param>
    /// <param name="text">The message.</param>
    /// <param name="schema">The answer's JSON schema.</param>
    /// <param name="task">What it did, for the log.</param>
    /// <param name="cancellationToken">Stops the process.</param>
    /// <returns>The structured answer as JSON.</returns>
    /// <exception cref="InvalidOperationException">Claude Code failed or gave no structured answer.</exception>
    private async Task<string> AskAsync(string system, string text, Dictionary<string, JsonElement> schema, string task, CancellationToken cancellationToken)
    {
        var promptFile = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(promptFile, system, cancellationToken);
            string[] arguments =
            [
                G.Print, G.InputFormat, G.StreamJson, G.OutputFormat, G.StreamJson, G.Verbose,
                G.Model, RB.Model,
                G.SystemPromptFile, promptFile,
                G.JsonSchema, JsonSerializer.Serialize(schema),
                G.Tools, G.NoTools,
                G.MaxTurns, CC.MaxTurns.ToString(CultureInfo.InvariantCulture),
                G.NoSessionPersistence,
                G.StrictMcpConfig,
                G.Settings, CC.HookFreeSettings,
            ];
            var started = Stopwatch.GetTimestamp();
            var printed = await runner.RunAsync(arguments, ClaudeCodeMessage.User(ClaudeCodeMessage.Text(text)), cancellationToken);
            var seconds = Stopwatch.GetElapsedTime(started).TotalSeconds;
            var result = ClaudeCodeInventoryProvider.Read(printed);

            LogUsage(task, result.Model, result.InputTokens + result.CacheReadTokens + result.CacheWriteTokens, result.OutputTokens, seconds);

            return result.Answer;
        }
        finally
        {
            File.Delete(promptFile);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Claude Code ({Model}) {Task} with {InputTokens} input and {OutputTokens} output tokens in {Seconds:0.0} s")]
    private partial void LogUsage(string task, string model, long inputTokens, long outputTokens, double seconds);
}
