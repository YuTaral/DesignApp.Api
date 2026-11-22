using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Objects;
using Microsoft.Extensions.Options;

using static DesignApp.Domain.Constants;

namespace DesignApp.Infrastructure.Ai.Fakes;

/// <summary>The free stand-in for reading a room photo into a plan: the same rough living room for every photo.</summary>
/// <param name="options">The fake step delay.</param>
internal sealed class FakePhotoPlanner(IOptions<AiOptions> options) : IPhotoPlanner
{
    /// <summary>
    /// The same plan for every photo: a 4 × 5 m living room with a window, a door, a sofa facing a TV stand with the TV
    /// above it, a coffee table, a rug and an armchair. Placed roughly, as a real answer is: the armchair overlaps the
    /// sofa, the TV stand stands a little off the wall and the TV crosses the window, for the builder to tidy.
    /// </summary>
    /// <param name="image">The photo (not looked at).</param>
    /// <param name="catalog">The objects it may use; those missing from it are left out.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    public async Task<Result<PhotoPlan>> PlanFromPhotoAsync(AnalysisImage image, IReadOnlyList<CatalogObject> catalog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        await Task.Delay(options.Value.FakeStepDelay, cancellationToken);
        var keys = catalog.Select(o => o.Key).ToHashSet();
        var objects = FakePlan.Where(o => keys.Contains(o.Key)).ToList();

        return new PhotoPlan(RoomType.LivingRoom, 4, 5, 2.6, "#E8E2D6", WallFinish.Paint, "#B89B7A", FloorType.Wood, CeilingLight.Pendant, new OutlinePoint(200, 470), objects);
    }

    /// <summary>The fake photo's objects, as <see cref="PlanFromPhotoAsync"/> describes them.</summary>
    private static readonly PhotoPlanItem[] FakePlan =
    [
        new("window", "standard", 0, 0, 0, 120, 10, 140, "#F4F1EA", "uPVC", PlanWall.Back, 300, 90),
        new("door", "standard", 0, 0, 0, 90, 5, 210, "#F4F1EA", "painted wood", PlanWall.Left, 60, 0),
        new("tv-stand", "standard", 200, 40, 0, 160, 40, 50, "#3A3633", "oak veneer", null, 0, 0),
        new("wall-tv", "standard", 0, 0, 0, 123, 5, 71, "#2A2A2A", "", PlanWall.Back, 230, 100),
        new("sofa", "standard", 200, 400, 180, 220, 95, 85, "#8A8580", "velvet", null, 0, 0),
        new("armchair", "standard", 300, 380, 180, 85, 85, 85, "#A8B89A", "linen", null, 0, 0),
        new("coffee-table", "standard", 200, 260, 0, 110, 60, 42, "#B8864B", "oak", null, 0, 0),
        new("rug", "standard", 200, 280, 0, 200, 300, 1, "#D8C3A5", "wool", null, 0, 0),
    ];
}
