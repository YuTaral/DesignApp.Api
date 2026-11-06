using DesignApp.Application.Analysis;
using DesignApp.Domain.Rooms;
using Microsoft.Extensions.Options;

namespace DesignApp.Infrastructure.Ai.Fakes;

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
