using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;

using CC = DesignApp.Infrastructure.Constants.Ai.ClaudeCode;
using G = DesignApp.Infrastructure.Constants.Ai.ClaudeCode.Flags;

namespace DesignApp.Infrastructure.Ai.ClaudeCode;

/// <summary>
/// Asks Claude Code (<c>claude -p</c>) one question with a structured answer, without tools, sessions or the
/// developer's hooks: the room builder's calls on the developer's Claude subscription share it.
/// </summary>
/// <param name="runner">Runs the <c>claude -p</c> process.</param>
/// <param name="logger">Records the tokens each call used.</param>
internal sealed partial class ClaudeCodeAsker(IClaudeCodeRunner runner, ILogger<ClaudeCodeAsker> logger)
{
    /// <summary>Runs Claude Code once with instructions, a message and an answer schema.</summary>
    /// <param name="model">The model.</param>
    /// <param name="system">The instructions.</param>
    /// <param name="message">The stream-json user message (text, and a photo if any).</param>
    /// <param name="schema">The answer's JSON schema.</param>
    /// <param name="task">What it did, for the log.</param>
    /// <param name="cancellationToken">Stops the process.</param>
    /// <returns>The structured answer as JSON.</returns>
    /// <exception cref="InvalidOperationException">Claude Code failed or gave no structured answer.</exception>
    public async Task<string> AskAsync(string model, string system, string message, Dictionary<string, JsonElement> schema, string task, CancellationToken cancellationToken)
    {
        var promptFile = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(promptFile, system, cancellationToken);
            string[] arguments =
            [
                G.Print, G.InputFormat, G.StreamJson, G.OutputFormat, G.StreamJson, G.Verbose,
                G.Model, model,
                G.SystemPromptFile, promptFile,
                G.JsonSchema, JsonSerializer.Serialize(schema),
                G.Tools, G.NoTools,
                G.MaxTurns, CC.MaxTurns.ToString(CultureInfo.InvariantCulture),
                G.NoSessionPersistence,
                G.StrictMcpConfig,
                G.Settings, CC.HookFreeSettings,
            ];
            var started = Stopwatch.GetTimestamp();
            var printed = await runner.RunAsync(arguments, message, cancellationToken);
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
