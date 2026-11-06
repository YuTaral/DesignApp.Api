using DesignApp.Infrastructure.Ai;
using DesignApp.Infrastructure.Ai.Claude;
using AiDefaults = DesignApp.Infrastructure.Constants.Ai.Defaults;

namespace DesignApp.UnitTests.Analysis;

/// <summary>The stored AI settings: the prompt's limits are filled in, and hand-edited values are checked.</summary>
public class AiSettingsTests
{
    [Fact]
    public void The_default_prompt_names_the_default_limits()
    {
        var prompt = new AiSettings().SystemPrompt();

        Assert.Contains($"2 or {AiDefaults.MaxPoints} points", prompt, StringComparison.Ordinal);
        Assert.Contains($"At most {AiDefaults.MaxListedObjects} objects", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("{", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void The_prompt_follows_the_stored_limits()
    {
        var prompt = new AiSettings { MaxPoints = 5, MaxListedObjects = 12 }.SystemPrompt();

        Assert.Contains("2 or 5 points", prompt, StringComparison.Ordinal);
        Assert.Contains("At most 12 objects", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void The_defaults_can_be_used()
    {
        Assert.Empty(new AiSettings().Problems());
    }

    [Fact]
    public void Each_unusable_value_is_named()
    {
        var problems = new AiSettings
        {
            InventoryModel = " ",
            MaxTokens = 0,
            MaxListedObjects = 500,
            SegmentationEndpoint = "fal.run/sam",
            MinFit = 1.5,
            MinShareInsideBox = -0.1,
        }.Problems();

        Assert.Equal(6, problems.Count);
        Assert.Contains(problems, p => p.Contains(nameof(AiSettings.InventoryModel), StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains(nameof(AiSettings.SegmentationEndpoint), StringComparison.Ordinal));
    }

    [Fact]
    public void Claude_answers_keep_at_most_the_stored_points()
    {
        var result = InventoryJson.Parse("""
            {"isRoom": true, "objects": [{"label": "back wall", "category": "Wall",
              "box": {"x": 0, "y": 0, "width": 1, "height": 0.6}, "color": "#EEEEEE",
              "points": [{"x": 0.1, "y": 0.1}, {"x": 0.5, "y": 0.2}, {"x": 0.9, "y": 0.3}]}]}
            """, maxPoints: 2);

        Assert.Equal(2, Assert.Single(result.Value).Points!.Count);
    }

    [Fact]
    public void The_schema_names_the_stored_points_limit()
    {
        var schema = InventoryJson.Schema(maxPoints: 4)["properties"].GetRawText();

        Assert.Contains("2 or 4 spots", schema, StringComparison.Ordinal);
    }
}
