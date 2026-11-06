using DesignApp.Application.Analysis;
using DesignApp.Application.Common;
using DesignApp.Domain.Common;
using DesignApp.Domain.Rooms;
using Microsoft.Extensions.Options;

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

/// <summary>
/// Stands in for SAM 3. A sample room photo gets its hand-traced outlines. For any other photo each
/// item is outlined from its box alone: walls and floors fill their box, everything else becomes an
/// ellipse inside it, so outlines look like objects rather than boxes.
/// </summary>
/// <param name="options">Supplies the delay that makes the progress UI visible.</param>
internal sealed class FakeSegmentationProvider(IOptions<AiOptions> options) : ISegmentationProvider
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<ObjectMask?>> SegmentAsync(
        AnalysisImage image, IReadOnlyList<InventoryItem> items, CancellationToken cancellationToken)
    {
        await Task.Delay(options.Value.FakeStepDelay, cancellationToken);
        var sample = SampleRooms.Match(image);
        return [.. items.Select(item => sample?.Outline(item.Label, image.Width, image.Height) ?? Outline(image.Width, image.Height, item))];
    }

    /// <summary>Draws one item's outline from its box, for photos that aren't samples.</summary>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="item">The item to outline.</param>
    internal static ObjectMask Outline(int width, int height, InventoryItem item)
    {
        var coverage = new byte[width * height];
        var (left, top) = (item.Box.X * width, item.Box.Y * height);
        var (boxWidth, boxHeight) = (item.Box.Width * width, item.Box.Height * height);
        var filled = item.Category.IsSurface();

        for (var y = (int)top; y < Math.Min(height, (int)Math.Ceiling(top + boxHeight)); y++)
        {
            for (var x = (int)left; x < Math.Min(width, (int)Math.Ceiling(left + boxWidth)); x++)
            {
                var dx = (x + 0.5 - left) / boxWidth * 2 - 1;
                var dy = (y + 0.5 - top) / boxHeight * 2 - 1;
                if (filled || dx * dx + dy * dy <= 1)
                    coverage[y * width + x] = Constants.Imaging.FullCoverage;
            }
        }
        return new ObjectMask(width, height, coverage);
    }
}
