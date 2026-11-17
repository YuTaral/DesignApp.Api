using DesignApp.Infrastructure.Ai.ClaudeCode;

namespace DesignApp.UnitTests;

/// <summary>A Claude Code that prints a fixed result and remembers what it was asked.</summary>
/// <param name="printed">What every call prints.</param>
internal sealed class CannedClaudeCode(string printed) : IClaudeCodeRunner
{
    /// <summary>The last call's arguments.</summary>
    public IReadOnlyList<string> Arguments { get; private set; } = [];

    /// <summary>The last call's stream-json message.</summary>
    public string Input { get; private set; } = "";

    /// <summary>The system prompt file's text, read during the call (the provider deletes the file afterwards).</summary>
    public string SystemPrompt { get; private set; } = "";

    /// <inheritdoc/>
    public Task<string> RunAsync(IReadOnlyList<string> arguments, string input, CancellationToken cancellationToken)
    {
        Arguments = arguments;
        Input = input;
        var file = arguments.SkipWhile(a => a != "--system-prompt-file").Skip(1).First();

        SystemPrompt = File.ReadAllText(file);

        return Task.FromResult(printed);
    }
}
