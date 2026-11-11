namespace DesignApp.Infrastructure.Ai.ClaudeCode;

/// <summary>The useful part of one Claude Code result.</summary>
/// <param name="Answer">The structured answer, as JSON.</param>
/// <param name="Model">The model that answered.</param>
/// <param name="InputTokens">Its input tokens (not counting the prompt cache).</param>
/// <param name="OutputTokens">Its output tokens.</param>
/// <param name="CacheReadTokens">Its input tokens read from the prompt cache.</param>
/// <param name="CacheWriteTokens">Its input tokens written to the prompt cache.</param>
/// <param name="CostUsd">Claude Code's estimate of what the API would have charged (the subscription is billed instead).</param>
internal sealed record ClaudeCodeResult(
    string Answer, string Model, long InputTokens, long OutputTokens, long CacheReadTokens, long CacheWriteTokens, double CostUsd);
