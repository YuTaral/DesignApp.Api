using DesignApp.Application.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.Interfaces;

/// <summary>Hands out the AI providers of a mode: the fakes or Claude and SAM 3.</summary>
public interface IAiProviders
{
    /// <summary>Which modes can be used.</summary>
    AiAvailability Availability { get; }

    /// <summary>Step 1 of a mode.</summary>
    /// <param name="mode">Fake or real.</param>
    /// <exception cref="InvalidOperationException">The mode isn't set up.</exception>
    IObjectInventoryProvider Inventory(AiMode mode);

    /// <summary>Step 2 of a mode.</summary>
    /// <param name="mode">Fake or real.</param>
    /// <exception cref="InvalidOperationException">The mode isn't set up.</exception>
    ISegmentationProvider Segmentation(AiMode mode);

    /// <summary>Removes objects from photos with the given AI: the free fake or the real eraser.</summary>
    /// <param name="mode">The room's AI mode.</param>
    IObjectRemovalProvider Removal(AiMode mode);
}
