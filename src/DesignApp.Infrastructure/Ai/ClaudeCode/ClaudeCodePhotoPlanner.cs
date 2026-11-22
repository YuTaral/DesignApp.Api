using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Infrastructure.Ai.Claude;

using P = DesignApp.Infrastructure.Constants.Ai.RoomBuilder.Photo;

namespace DesignApp.Infrastructure.Ai.ClaudeCode;

/// <summary>
/// Reads a room photo into a plan through Claude Code on the developer's Claude subscription, in Development only: the
/// same instructions and answer schema as <see cref="ClaudePhotoPlanner"/>.
/// </summary>
/// <param name="asker">Asks Claude Code.</param>
internal sealed class ClaudeCodePhotoPlanner(ClaudeCodeAsker asker) : IPhotoPlanner
{
    /// <inheritdoc/>
    public async Task<Result<PhotoPlan>> PlanFromPhotoAsync(AnalysisImage image, IReadOnlyList<CatalogObject> catalog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        var message = ClaudeCodeInventoryProvider.Message(image, PhotoPlanJson.Message(catalog, image.Width, image.Height));
        var answer = await asker.AskAsync(P.Model, P.System, message, PhotoPlanJson.Schema(catalog), "read a photo into a plan", cancellationToken);

        return PhotoPlanJson.Parse(answer);
    }
}
