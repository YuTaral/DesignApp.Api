using DesignApp.Application.Analysis;
using DesignApp.Domain.Rooms;
using DesignApp.Infrastructure.Ai.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DesignApp.Infrastructure.Ai;

/// <summary>
/// Hands out the fake or the real providers per room. The fakes always exist; the real ones only
/// when both keys are set, and are created the first time a room asks for them.
/// </summary>
/// <param name="services">Resolves the providers.</param>
/// <param name="options">The AI settings.</param>
internal sealed class AiProviders(IServiceProvider services, IOptions<AiOptions> options) : IAiProviders
{
    /// <summary>Registration key of the real providers.</summary>
    internal const string RealKey = "real";

    /// <inheritdoc/>
    public AiAvailability Availability { get; } = new(
        options.Value.UseFakes ? AiMode.Fake : AiMode.Real,
        options.Value.AllowAiSwitch,
        options.Value.HasRealKeys);

    /// <inheritdoc/>
    public IObjectInventoryProvider Inventory(AiMode mode) => mode is AiMode.Fake
        ? services.GetRequiredService<FakeInventoryProvider>()
        : services.GetKeyedService<IObjectInventoryProvider>(RealKey) ?? throw NotSetUp();

    /// <inheritdoc/>
    public ISegmentationProvider Segmentation(AiMode mode) => mode is AiMode.Fake
        ? services.GetRequiredService<FakeSegmentationProvider>()
        : services.GetKeyedService<ISegmentationProvider>(RealKey) ?? throw NotSetUp();

    /// <summary>The error for a real analysis without keys (uploads are refused before this can happen).</summary>
    private static InvalidOperationException NotSetUp() =>
        new("The real AI isn't set up: Ai:AnthropicApiKey and Ai:FalKey are needed.");
}
