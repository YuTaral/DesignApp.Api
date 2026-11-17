using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace DesignApp.Infrastructure.Ai.ClaudeCode;

/// <summary>
/// Runs Claude Code as a child process in an empty folder (so no project's settings, hooks or CLAUDE.md load),
/// without <c>ANTHROPIC_API_KEY</c> in its environment, so it uses the subscription the developer signed in with.
/// </summary>
/// <param name="options">Where the Claude Code command is.</param>
internal sealed class ClaudeCodeRunner(IOptions<AiOptions> options) : IClaudeCodeRunner
{
    /// <inheritdoc/>
    public async Task<string> RunAsync(IReadOnlyList<string> arguments, string input, CancellationToken cancellationToken)
    {
        var path = options.Value.ClaudeCodePath;
        var folder = Path.Combine(Path.GetTempPath(), Constants.Ai.ClaudeCode.WorkingFolder);

        Directory.CreateDirectory(folder);
        var start = new ProcessStartInfo(path)
        {
            WorkingDirectory = folder,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment.Remove(Constants.Ai.ClaudeCode.ApiKeyVariable);

        using var process = new Process { StartInfo = start };

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            throw new InvalidOperationException(Constants.Ai.ClaudeCode.Messages.NotStarted(path, e.Message), e);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(Constants.Ai.ClaudeCode.Timeout);

        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errors = process.StandardError.ReadToEndAsync(timeout.Token);

            await process.StandardInput.WriteLineAsync(input.AsMemory(), timeout.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            var printed = await output;

            if (string.IsNullOrWhiteSpace(printed))
            {
                throw new InvalidOperationException(Constants.Ai.ClaudeCode.Messages.Failed((await errors).Trim()));
            }

            return printed;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(Constants.Ai.ClaudeCode.Messages.TimedOut);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
    }
}
