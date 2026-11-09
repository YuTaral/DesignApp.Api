using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Objects;
using Microsoft.Extensions.Options;
using static DesignApp.Domain.Constants;

namespace DesignApp.Infrastructure.Ai.Fakes;

/// <summary>
/// Stands in for Claude in development and tests, for free and offline. A sample room photo
/// (see <see cref="SampleRooms"/>) gets its own hand-traced objects; any other photo gets the same
/// plausible <see cref="Layout"/> (walls above, floor below, a sofa, a rug, a lamp and a plant).
/// </summary>
/// <param name="options">Supplies the delay that makes the progress UI visible.</param>
internal sealed class FakeInventoryProvider(IOptions<AiOptions> options) : IObjectInventoryProvider
{
    /// <summary>The layout every photo that isn't a sample gets.</summary>
    internal static readonly IReadOnlyList<InventoryItem> Layout =
    [
        new("back wall", ObjectCategory.Wall, new BoundingBox(0, 0, 1, 0.62), HexColor.Parse("#E8E2D6")),
        new("floor", ObjectCategory.Floor, new BoundingBox(0, 0.62, 1, 0.38), HexColor.Parse("#B89B7A")),
        new("sofa", ObjectCategory.Furniture, new BoundingBox(0.22, 0.42, 0.5, 0.3), HexColor.Parse("#7D8B6A")),
        new("rug", ObjectCategory.Textile, new BoundingBox(0.15, 0.74, 0.62, 0.2), HexColor.Parse("#C9A27E")),
        new("floor lamp", ObjectCategory.Lighting, new BoundingBox(0.78, 0.2, 0.08, 0.55), HexColor.Parse("#2F2C2A")),
        new("potted plant", ObjectCategory.Decor, new BoundingBox(0.04, 0.4, 0.14, 0.35), HexColor.Parse("#4F6B3A")),
    ];

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<InventoryItem>>> ListObjectsAsync(AnalysisImage image, CancellationToken cancellationToken)
    {
        await Task.Delay(options.Value.FakeStepDelay, cancellationToken);
        return Result.Success(SampleRooms.Match(image)?.Inventory() ?? Layout);
    }
}
