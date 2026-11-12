using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Infrastructure.Library;

/// <summary>One object as a library room's <c>.objects.json</c> stores it: the box in photo pixels, the color as hex.</summary>
/// <param name="Index">The object's value in the label map.</param>
/// <param name="Label">What it is, such as "sofa".</param>
/// <param name="Category">Its kind.</param>
/// <param name="X">Left edge of its box.</param>
/// <param name="Y">Top edge of its box.</param>
/// <param name="Width">Width of its box.</param>
/// <param name="Height">Height of its box.</param>
/// <param name="Color">Its main color, such as <c>#a0b1c2</c>.</param>
/// <param name="Hand">Whether it was marked by hand.</param>
internal sealed record LibraryObjectFile(int Index, string Label, ObjectCategory Category, double X, double Y, double Width, double Height, string Color, bool Hand = false)
{
    /// <summary>The stored form of a detected object.</summary>
    /// <param name="o">The object.</param>
    public static LibraryObjectFile From(DetectedObject o)
    {
        return new(o.Index, o.Label, o.Category, o.Box.X, o.Box.Y, o.Box.Width, o.Box.Height, o.DominantColor.Value, o.MarkedByHand);
    }

    /// <summary>The detected object it stores.</summary>
    public DetectedObject ToDomain()
    {
        return new(Index, Label, Category, new BoundingBox(X, Y, Width, Height), HexColor.Parse(Color), Hand);
    }
}
