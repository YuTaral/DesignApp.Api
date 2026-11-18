using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests;

/// <summary>A room builder AI that reads every photo into the same given plan (and describes nothing).</summary>
/// <param name="plan">The plan every photo is read into.</param>
internal sealed class FixedPhotoPlanner(PhotoPlan plan) : IRoomBuilderAi
{
    /// <inheritdoc/>
    public Task<Result<CatalogObject>> DescribeObjectAsync(RoomType roomType, string description, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("This test describes no objects.");
    }

    /// <inheritdoc/>
    public Task<Result<PhotoPlan>> PlanFromPhotoAsync(AnalysisImage image, IReadOnlyList<CatalogObject> catalog, CancellationToken cancellationToken)
    {
        return Task.FromResult<Result<PhotoPlan>>(plan);
    }
}
