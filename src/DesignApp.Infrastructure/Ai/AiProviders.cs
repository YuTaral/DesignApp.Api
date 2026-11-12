using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Infrastructure.Ai.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static DesignApp.Domain.Constants;

namespace DesignApp.Infrastructure.Ai;

/// <summary>
/// Hands out the fake or the real providers per room. The fakes always exist; the real ones only
/// when both keys are set, and are created the first time a room asks for them.
/// </summary>
/// <param name="services">Resolves the providers.</param>
/// <param name="options">The AI settings.</param>
internal sealed class AiProviders(IServiceProvider services, IOptions<AiOptions> options) : IAiProviders
{
    /// <inheritdoc/>
    public AiAvailability Availability { get; } = new(
        options.Value.UseFakes ? AiMode.Fake : AiMode.Real,
        options.Value.AllowAiSwitch,
        options.Value.HasRealKeys);

    /// <inheritdoc/>
    public IObjectInventoryProvider Inventory(AiMode mode)
    {
        return mode is AiMode.Fake
            ? services.GetRequiredService<FakeInventoryProvider>()
            : services.GetKeyedService<IObjectInventoryProvider>(Constants.Ai.RealKey) ?? throw NotSetUp();
    }

    /// <inheritdoc/>
    public ISegmentationProvider Segmentation(AiMode mode)
    {
        return mode is AiMode.Fake
            ? services.GetRequiredService<FakeSegmentationProvider>()
            : services.GetKeyedService<ISegmentationProvider>(Constants.Ai.RealKey) ?? throw NotSetUp();
    }

    /// <summary>The error for a real analysis without keys (uploads are refused before this can happen).</summary>
    /// <inheritdoc/>
    public IObjectRemovalProvider Removal(AiMode mode)
    {
        return mode is AiMode.Fake
            ? services.GetRequiredService<FakeObjectRemovalProvider>()
            : services.GetKeyedService<IObjectRemovalProvider>(Constants.Ai.RealKey) ?? throw NotSetUp();
    }

    private static InvalidOperationException NotSetUp()
    {
        return new(Constants.Config.RealAiNotSetUp);
    }
}
