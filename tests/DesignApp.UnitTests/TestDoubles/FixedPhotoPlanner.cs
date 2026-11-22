using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;

namespace DesignApp.UnitTests;

/// <summary>A photo planner that reads every photo into the same given plan.</summary>
/// <param name="plan">The plan every photo is read into.</param>
internal sealed class FixedPhotoPlanner(PhotoPlan plan) : IPhotoPlanner
{
    /// <inheritdoc/>
    public Task<Result<PhotoPlan>> PlanFromPhotoAsync(AnalysisImage image, IReadOnlyList<CatalogObject> catalog, CancellationToken cancellationToken)
    {
        return Task.FromResult<Result<PhotoPlan>>(plan);
    }
}
