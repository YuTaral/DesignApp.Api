using DesignApp.Application.Objects;


namespace DesignApp.Application.Interfaces;

/// <summary>Step 1 of the analysis (Claude): lists the objects in the photo.</summary>
public interface IObjectInventoryProvider
{
    /// <summary>Lists the objects worth restyling in a room photo.</summary>
    /// <param name="image">The photo to look at.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The objects, or a failure the user can read when the photo isn't of a room.</returns>
    Task<Result<IReadOnlyList<InventoryItem>>> ListObjectsAsync(AnalysisImage image, CancellationToken cancellationToken);
}
