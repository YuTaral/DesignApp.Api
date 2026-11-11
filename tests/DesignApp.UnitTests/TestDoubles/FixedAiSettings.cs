using DesignApp.Infrastructure.Ai;

namespace DesignApp.UnitTests;

/// <summary>AI settings that never change, without a database.</summary>
/// <param name="settings">The settings to return; the defaults when omitted.</param>
internal sealed class FixedAiSettings(AiSettings? settings = null) : IAiSettingsStore
{
    /// <summary>The settings every call returns.</summary>
    public AiSettings Settings { get; } = settings ?? new AiSettings();

    /// <inheritdoc/>
    public Task<AiSettings> GetAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Settings);
    }
}
