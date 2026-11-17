using DesignApp.Domain.Objects;
using SkiaSharp;
using static DesignApp.Domain.Constants;

namespace DesignApp.Infrastructure.Ai.Fakes;

/// <summary>One traced object.</summary>
/// <param name="Label">What it is.</param>
/// <param name="Category">The kind of object.</param>
/// <param name="Shapes">Its outline: the union of these shapes.</param>
internal sealed record SampleObject(string Label, ObjectCategory Category, IReadOnlyList<SampleShape> Shapes)
{
    /// <summary>The bounding box of all shapes, normalized and clipped to the photo.</summary>
    /// <param name="width">The photo's width.</param>
    /// <param name="height">The photo's height.</param>
    public BoundingBox Box(int width, int height)
    {
        var bounds = Shapes.Select(s => s.Bounds()).Aggregate(SKRect.Union);

        bounds.Intersect(new SKRect(0, 0, width, height));

        return new BoundingBox(bounds.Left / width, bounds.Top / height, bounds.Width / width, bounds.Height / height);
    }
}
