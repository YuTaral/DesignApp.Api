using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Extensions;
using DesignApp.Infrastructure.Photos;
using Microsoft.Extensions.Options;

namespace DesignApp.Infrastructure.Ai.Fakes;

/// <summary>
/// The free depth for development and tests: the room as a box (walls at the sides, floor, ceiling and a back wall),
/// with each object standing on the floor where its outline's bottom edge is. Not the real shape, but enough to build
/// and test the 3D view without paying.
/// </summary>
/// <param name="options">The fake step delay.</param>
internal sealed class FakeDepthProvider(IOptions<AiOptions> options) : IDepthProvider
{
    /// <inheritdoc/>
    public async Task<Result<DepthMeasurement>> MeasureAsync(DepthInput input, CancellationToken cancellationToken)
    {
        await Task.Delay(options.Value.FakeStepDelay, cancellationToken);
        var (labels, labelWidth, labelHeight) = SkiaRoomImaging.DecodeGrayPng(input.LabelMap);
        var (width, height) = (Constants.Ai.Depth.GridWidth, DepthGrid.HeightFor(input.Width, input.Height));
        var fovX = Constants.Ai.Depth.FakeFovX;
        var tanX = Math.Tan(fovX * Math.PI / 360);
        var tanY = tanX * input.Height / input.Width;
        var fovY = Math.Atan(tanY) * 360 / Math.PI;
        var objects = input.Objects.Where(o => !o.Category.IsSurface()).ToDictionary(o => o.Index);

        var grid = new float[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var (u, v) = ((x + 0.5) / width, (y + 0.5) / height);
                var depth = BoxDepth((2 * u - 1) * tanX, (2 * v - 1) * tanY);
                var label = labels[(int)(v * labelHeight) * labelWidth + (int)(u * labelWidth)];
                // An object stands on the floor at its bottom edge, so it sits in front of the wall behind it.
                if (objects.TryGetValue(label, out var detected))
                    depth = Math.Min(depth, BoxDepth(0, (2 * Math.Min(1, detected.Box.Y + detected.Box.Height) - 1) * tanY));
                grid[y * width + x] = (float)depth;
            }
        }
        return new DepthMeasurement(DepthGrid.ToPng(grid, width, height), fovX, fovY);
    }

    /// <summary>How far a ray goes before it meets the box room.</summary>
    /// <param name="slopeX">The ray's sideways slope (x / z).</param>
    /// <param name="slopeY">The ray's downward slope (y / z).</param>
    /// <returns>The depth (z) where it meets a wall, the floor, the ceiling or the back wall.</returns>
    private static double BoxDepth(double slopeX, double slopeY)
    {
        var depth = Constants.Ai.Depth.FakeBackWall;
        if (Math.Abs(slopeX) > 1e-9)
            depth = Math.Min(depth, Constants.Ai.Depth.FakeHalfWidth / Math.Abs(slopeX));
        if (slopeY > 1e-9)
            depth = Math.Min(depth, Constants.Ai.Depth.FakeFloor / slopeY);
        else if (slopeY < -1e-9)
            depth = Math.Min(depth, Constants.Ai.Depth.FakeCeiling / -slopeY);
        return depth;
    }
}
