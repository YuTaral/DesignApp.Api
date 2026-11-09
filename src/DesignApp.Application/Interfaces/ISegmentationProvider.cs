using DesignApp.Application.Objects;

namespace DesignApp.Application.Interfaces;

/// <summary>Step 2 of the analysis (SAM 3): outlines each listed object.</summary>
public interface ISegmentationProvider
{
    /// <summary>Outlines the given objects in the photo.</summary>
    /// <param name="image">The photo the items were found in.</param>
    /// <param name="items">The objects to outline.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    /// <returns>One entry per item, in the same order; <c>null</c> where the object couldn't be outlined.</returns>
    Task<IReadOnlyList<ObjectMask?>> SegmentAsync(AnalysisImage image, IReadOnlyList<InventoryItem> items, CancellationToken cancellationToken);
}
