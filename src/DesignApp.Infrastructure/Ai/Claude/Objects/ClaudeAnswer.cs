using Anthropic.Models.Messages;

namespace DesignApp.Infrastructure.Ai.Claude;

/// <summary>Reads Claude's answers.</summary>
internal static class ClaudeAnswer
{
    /// <summary>The text of an answer: its text blocks joined.</summary>
    /// <param name="response">The answer.</param>
    public static string Text(Message response)
    {
        return string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
    }
}
