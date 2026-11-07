namespace DesignApp.Infrastructure.Ai;

/// <summary>Settings for the AI pipeline, read from the <c>Ai</c> configuration section.</summary>
public sealed class AiOptions
{
    /// <summary>
    /// Whether uploads that don't choose use the built-in fake providers instead of Claude and SAM 3
    /// (free, offline and instant). On in Development.
    /// </summary>
    public bool UseFakes { get; set; }

    /// <summary>
    /// Whether each upload may choose fake or real AI (the switch above the upload area). On in
    /// Development only, so production always uses the real AI.
    /// </summary>
    public bool AllowAiSwitch { get; set; }

    /// <summary>Whether both keys of the real AI are set.</summary>
    public bool HasRealKeys => !string.IsNullOrWhiteSpace(AnthropicApiKey) && !string.IsNullOrWhiteSpace(FalKey);

    /// <summary>How long each fake step waits, so the progress UI can be seen. Ignored by the real providers.</summary>
    public TimeSpan FakeStepDelay { get; set; } = TimeSpan.Zero;

    /// <summary>The Anthropic API key (user secrets locally). Required unless <see cref="UseFakes"/> is on.</summary>
    public string AnthropicApiKey { get; set; } = "";

    /// <summary>
    /// The Anthropic workspace to bill, sent as the <c>anthropic-workspace-id</c> header. Only needed
    /// for an organization-level key (<c>sk-ant-usr-...</c>); a key created inside a workspace already knows it.
    /// </summary>
    public string? AnthropicWorkspaceId { get; set; }



    /// <summary>The fal.ai API key (user secrets locally). Required unless <see cref="UseFakes"/> is on.</summary>
    public string FalKey { get; set; } = "";



    /// <summary>
    /// When set (with the fakes off), real analyses of the sample photos are saved in this folder
    /// as <c>name.sam3.json</c>, ready to become free fake data. Development use only.
    /// </summary>
    public string? RecordSamplesTo { get; set; }

    /// <summary>How many rooms are analyzed at the same time.</summary>
    public int MaxConcurrentAnalyses { get; set; } = Constants.Ai.DefaultMaxConcurrentAnalyses;
}
