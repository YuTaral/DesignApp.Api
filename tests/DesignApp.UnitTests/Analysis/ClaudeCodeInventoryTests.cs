using System.Text.Json;
using DesignApp.Application.Objects;
using DesignApp.Infrastructure.Ai;
using DesignApp.Infrastructure.Ai.ClaudeCode;
using Microsoft.Extensions.Logging.Abstractions;
using static DesignApp.Infrastructure.Constants;

namespace DesignApp.UnitTests.Analysis;

/// <summary>
/// The development route to Claude through Claude Code on the subscription: it must ask exactly what the API
/// route asks (model, effort, system prompt, photo, schema), with no tools, and read Claude Code's JSON result.
/// </summary>
public class ClaudeCodeInventoryTests
{
    private const string Answer = """
        {"isRoom": true, "objects": [
          {"label": "grey sofa", "category": "Furniture", "box": {"x": 20, "y": 40, "width": 200, "height": 100}, "color": "#808080"}
        ]}
        """;

    private static string Printed(string answer, bool isError = false)
    {
        // Claude Code prints one JSON line per message; the result comes last.
        var result = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["type"] = "result",
            ["is_error"] = isError,
            ["result"] = "done",
            ["structured_output"] = JsonDocument.Parse(answer).RootElement,
            ["total_cost_usd"] = 0.021,
            ["modelUsage"] = new Dictionary<string, object>
            {
                ["claude-haiku-4-5"] = new { inputTokens = 50, outputTokens = 10 },
                ["claude-sonnet-5-5"] = new { inputTokens = 5500, outputTokens = 1300, cacheReadInputTokens = 0, cacheCreationInputTokens = 0 },
            },
        });

        return "{\"type\":\"system\",\"subtype\":\"init\"}\n{\"type\":\"assistant\"}\n" + result + "\n";
    }

    [Fact]
    public async Task It_asks_with_the_app_settings_and_no_tools_and_reads_the_objects()
    {
        var settings = new AiSettings { InventoryModel = "claude-haiku-5-5", InventoryEffort = InventoryEffort.Low };
        var claudeCode = new CannedClaudeCode(Printed(Answer));
        var provider = new ClaudeCodeInventoryProvider(claudeCode, new FixedAiSettings(settings), NullLogger<ClaudeCodeInventoryProvider>.Instance);

        var listed = await provider.ListObjectsAsync(new AnalysisImage([0xFF, 0xD8], 400, 200), CancellationToken.None);

        Assert.True(listed.Succeeded);
        var sofa = Assert.Single(listed.Value);

        Assert.Equal("grey sofa", sofa.Label);
        Assert.Equal(0.05, sofa.Box.X, 3);   // pixels became fractions of the 400 x 200 photo
        var args = claudeCode.Arguments;

        Assert.Equal("claude-haiku-5-5", args[args.ToList().IndexOf(Ai.ClaudeCode.Flags.Model) + 1]);
        Assert.Equal("low", args[args.ToList().IndexOf(Ai.ClaudeCode.Flags.Effort) + 1]);
        Assert.Equal(Ai.ClaudeCode.Flags.NoTools, args[args.ToList().IndexOf(Ai.ClaudeCode.Flags.Tools) + 1]);
        Assert.Equal(settings.SystemPrompt(), claudeCode.SystemPrompt);
        using var message = JsonDocument.Parse(claudeCode.Input);
        var content = message.RootElement.GetProperty("message").GetProperty("content");

        Assert.Equal("image", content[0].GetProperty("type").GetString());
        Assert.Equal(settings.UserMessage, content[1].GetProperty("text").GetString());
    }

    [Fact]
    public void The_model_that_did_the_work_and_its_tokens_are_read()
    {
        var result = ClaudeCodeInventoryProvider.Read(Printed(Answer));

        Assert.Equal("claude-sonnet-5-5", result.Model);
        Assert.Equal(5500, result.InputTokens);
        Assert.Equal(1300, result.OutputTokens);
        Assert.Equal(0.021, result.CostUsd);
    }

    [Fact]
    public void A_failed_run_or_one_without_a_structured_answer_throws()
    {
        Assert.Throws<InvalidOperationException>(() => ClaudeCodeInventoryProvider.Read(Printed(Answer, isError: true)));
        Assert.Throws<InvalidOperationException>(() => ClaudeCodeInventoryProvider.Read("""{"type":"result","is_error":false,"result":"Not logged in"}"""));
    }
}
