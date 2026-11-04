namespace DesignApp.Infrastructure.Ai;

/// <summary>Settings for the AI pipeline, read from the <c>Ai</c> configuration section.</summary>
public sealed class AiOptions
{
    /// <summary>The configuration section these options are read from.</summary>
    public const string Section = "Ai";

    /// <summary>
    /// Use built-in fake providers instead of Claude and SAM 3: free, offline and instant. On in
    /// Development; turn it off (with keys set) to try the real AI.
    /// </summary>
    public bool UseFakes { get; set; }

    /// <summary>How long each fake step waits, so the progress UI can be seen. Ignored by the real providers.</summary>
    public TimeSpan FakeStepDelay { get; set; } = TimeSpan.Zero;

    /// <summary>The Anthropic API key (user secrets locally). Required unless <see cref="UseFakes"/> is on.</summary>
    public string AnthropicApiKey { get; set; } = "";

    /// <summary>The Claude model that lists the objects.</summary>
    public string InventoryModel { get; set; } = "claude-haiku-5-5";

    /// <summary>The fal.ai API key (user secrets locally). Required unless <see cref="UseFakes"/> is on.</summary>
    public string FalKey { get; set; } = "";

    /// <summary>fal.ai's synchronous endpoint for SAM 3 image segmentation.</summary>
    public Uri SegmentationEndpoint { get; set; } = new("https://fal.run/fal-ai/sam-3/image");

    /// <summary>How many SAM 3 requests one analysis sends at once (one request per object).</summary>
    public int SegmentationParallelism { get; set; } = 4;

    /// <summary>How many rooms are analyzed at the same time.</summary>
    public int MaxConcurrentAnalyses { get; set; } = 2;
}
