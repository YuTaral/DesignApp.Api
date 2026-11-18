using DesignApp.Application.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.Interfaces;

/// <summary>The language model's part in the room builder: objects from descriptions, and plans from room photos.</summary>
public interface IRoomBuilderAi
{
    /// <summary>Turns the user's description of an object into one the builder can place, with its size and color.</summary>
    /// <param name="roomType">The room it is for.</param>
    /// <param name="description">The user's words, such as "a round oak coffee table, about 90 cm".</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The object, or a failure the user can read.</returns>
    Task<Result<CatalogObject>> DescribeObjectAsync(RoomType roomType, string description, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a room photo into a plan: the room's type, size and surfaces, and its objects as catalog objects placed on
    /// the plan, roughly (the builder tidies them).
    /// </summary>
    /// <param name="image">The photo, prepared for analysis.</param>
    /// <param name="catalog">The objects it may use (every room type's), by key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The plan as read, or a failure the user can read.</returns>
    Task<Result<PhotoPlan>> PlanFromPhotoAsync(AnalysisImage image, IReadOnlyList<CatalogObject> catalog, CancellationToken cancellationToken);
}
