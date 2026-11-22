using DesignApp.Application.Objects;

namespace DesignApp.Application.Interfaces;

/// <summary>Reads a room photo into a plan (Build a room's Start from a photo).</summary>
public interface IPhotoPlanner
{
    /// <summary>
    /// Reads a room photo into a plan: the room's type, size and surfaces, where the photo was taken from, and its
    /// objects as catalog objects placed on the plan, roughly (<c>PhotoPlanReader</c> tidies them).
    /// </summary>
    /// <param name="image">The photo, prepared for analysis.</param>
    /// <param name="catalog">The objects it may use (every room type's), by key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The plan as read, or a failure the user can read.</returns>
    Task<Result<PhotoPlan>> PlanFromPhotoAsync(AnalysisImage image, IReadOnlyList<CatalogObject> catalog, CancellationToken cancellationToken);
}
