using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests;

/// <summary>The same providers for every AI mode, so a test decides what "the AI" does.</summary>
/// <param name="inventory">Step 1.</param>
/// <param name="segmentation">Step 2.</param>
/// <param name="removal">Removing objects from photos; none unless a test needs it.</param>
internal sealed class FixedAiProviders(IObjectInventoryProvider inventory, ISegmentationProvider segmentation, IObjectRemovalProvider? removal = null)
    : IAiProviders
{
    /// <inheritdoc/>
    public AiAvailability Availability { get; } = new(AiMode.Fake, CanChoose: true, RealAvailable: true);

    /// <summary>The modes rooms asked for, in order.</summary>
    public List<AiMode> Asked { get; } = [];

    /// <inheritdoc/>
    public IObjectInventoryProvider Inventory(AiMode mode)
    {
        Asked.Add(mode);
        return inventory;
    }

    /// <inheritdoc/>
    public ISegmentationProvider Segmentation(AiMode mode)
    {
        return segmentation;
    }

    /// <inheritdoc/>
    public IObjectRemovalProvider Removal(AiMode mode)
    {
        return removal ?? throw new InvalidOperationException("This test has no object remover.");
    }
}
