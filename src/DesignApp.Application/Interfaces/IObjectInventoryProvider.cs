using DesignApp.Application.Objects;
using DesignApp.Domain.Objects;


namespace DesignApp.Application.Interfaces;

/// <summary>Step 1 of the analysis (Claude): lists the objects in the photo.</summary>
public interface IObjectInventoryProvider
{
    /// <summary>Lists the objects worth restyling in a room photo.</summary>
    /// <param name="image">The photo to look at.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The objects, or a failure the user can read when the photo isn't of a room.</returns>
    Task<Result<IReadOnlyList<InventoryItem>>> ListObjectsAsync(AnalysisImage image, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the parts of one object that can be colored on their own, such as a chair's seat, backrest and legs, each
    /// with its box (and spots on it) in the photo, for the segmentation to outline.
    /// </summary>
    /// <param name="image">The photo, as for the analysis.</param>
    /// <param name="whole">The object, with its name and box.</param>
    /// <param name="instructions">The user's own words on which parts to find (such as "the cushions on their own"), or <c>null</c>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The parts (none when the object has no separate parts), or a failure the user can read.</returns>
    Task<Result<IReadOnlyList<InventoryItem>>> ListPartsAsync(AnalysisImage image, DetectedObject whole, string? instructions, CancellationToken cancellationToken);
}
