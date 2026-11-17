using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;

namespace DesignApp.Application.UseCases;

/// <summary>
/// Turns the user's description of an object into one they can place in their built room, with the plan's AI (the
/// fake, or Claude Haiku). The object isn't added to the plan here: the user places it, then the plan is saved.
/// </summary>
/// <param name="plans">Where plans are stored.</param>
/// <param name="currentUser">The signed-in user or guest.</param>
/// <param name="ai">The AI providers.</param>
public sealed class DescribePlanObject(IPlanRepository plans, ICurrentUser currentUser, IAiProviders ai)
{
    /// <summary>Describes the object.</summary>
    /// <param name="planId">The plan it is for.</param>
    /// <param name="description">The user's words.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The object, a failure the user can read, or <c>null</c> when the user has no such plan.</returns>
    public async Task<Result<CatalogObject>?> HandleAsync(Guid planId, string? description, CancellationToken cancellationToken)
    {
        var plan = await plans.FindAsync(planId, currentUser.Id, cancellationToken);

        if (plan is null)
        {
            return null;
        }

        var words = description?.Trim() ?? "";

        if (words.Length is 0 or > Constants.Builder.MaxDescriptionLength)
        {
            return Result.Failure<CatalogObject>(Constants.Messages.ObjectDescription);
        }

        return await ai.Builder(plan.AiMode).DescribeObjectAsync(plan.RoomType, words, cancellationToken);
    }
}
