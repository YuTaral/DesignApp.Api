using System.Text.Json.Nodes;

using W = DesignApp.Infrastructure.Constants.Ai.ClaudeCode.Wire;

namespace DesignApp.Infrastructure.Ai.ClaudeCode;

/// <summary>Builds the user messages sent to Claude Code on its standard input, one stream-json line each.</summary>
internal static class ClaudeCodeMessage
{
    /// <summary>A user message as one stream-json line.</summary>
    /// <param name="content">Its parts, in order (see <see cref="Text"/>).</param>
    public static string User(params JsonNode[] content)
    {
        var message = new JsonObject
        {
            [W.Type] = W.User,
            [W.Message] = new JsonObject
            {
                [W.Role] = W.User,
                [W.Content] = new JsonArray(content),
            },
        };

        return message.ToJsonString();
    }

    /// <summary>A text part of a message.</summary>
    /// <param name="text">The text.</param>
    public static JsonObject Text(string text)
    {
        return new JsonObject { [W.Type] = W.Text, [W.Text] = text };
    }
}
