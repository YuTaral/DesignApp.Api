using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;

namespace DesignApp.Application.Helpers;

/// <summary>Outlines listed objects in a photo.</summary>
public static class Outlining
{
    /// <summary>Asks the segmentation for each item's mask, and keeps the items it outlined, each with its mask.</summary>
    /// <param name="segmentation">The segmentation to ask.</param>
    /// <param name="image">The analysis image.</param>
    /// <param name="items">The objects to outline.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The outlined items, in the items' order; an item without a mask is left out.</returns>
    public static async Task<List<SegmentedObject>> OutlineAsync(
        this ISegmentationProvider segmentation, AnalysisImage image, IReadOnlyList<InventoryItem> items, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(segmentation);
        var masks = await segmentation.SegmentAsync(image, items, cancellationToken);

        return items.Zip(masks)
            .Where(pair => pair.Second is not null)
            .Select(pair => new SegmentedObject(pair.First, pair.Second!))
            .ToList();
    }
}
