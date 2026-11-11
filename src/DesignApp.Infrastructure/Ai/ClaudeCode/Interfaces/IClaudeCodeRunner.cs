namespace DesignApp.Infrastructure.Ai.ClaudeCode;

/// <summary>Runs one headless Claude Code call (<c>claude -p</c>) and returns what it printed.</summary>
internal interface IClaudeCodeRunner
{
    /// <summary>Starts Claude Code with the arguments, writes <paramref name="input"/> to it and waits for its JSON result.</summary>
    /// <param name="arguments">The command-line arguments.</param>
    /// <param name="input">One stream-json line: the user message.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    /// <returns>Claude Code's standard output (its <c>--output-format json</c> result).</returns>
    Task<string> RunAsync(IReadOnlyList<string> arguments, string input, CancellationToken cancellationToken);
}
