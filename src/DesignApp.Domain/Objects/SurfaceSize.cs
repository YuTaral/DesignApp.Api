using DesignApp.Domain.Exceptions;

namespace DesignApp.Domain.Objects;

/// <summary>
/// The size the user gave a wall, floor or ceiling, so the app can say how much paint it needs. A wall is width ×
/// height; a floor or ceiling is width × length.
/// </summary>
public sealed record SurfaceSize
{
    /// <summary>The <see cref="DetectedObject.Index"/> of the surface.</summary>
    public int ObjectIndex { get; }

    /// <summary>The width, in metres.</summary>
    public double Width { get; }

    /// <summary>The height (or a floor's or ceiling's length), in metres.</summary>
    public double Height { get; }

    /// <summary>Creates a size.</summary>
    /// <param name="objectIndex">The surface.</param>
    /// <param name="width">The width, in metres.</param>
    /// <param name="height">The height or length, in metres.</param>
    /// <exception cref="DomainException">A side is outside 0.1 to 50 metres.</exception>
    public SurfaceSize(int objectIndex, double width, double height)
    {
        if (!InRange(width) || !InRange(height))
        {
            throw new DomainException(Constants.Messages.SurfaceSize);
        }

        (ObjectIndex, Width, Height) = (objectIndex, width, height);
    }

    /// <summary>The area, in square metres.</summary>
    public double Area => Width * Height;

    /// <summary>Whether a side is a number in the allowed range.</summary>
    /// <param name="metres">The side.</param>
    private static bool InRange(double metres)
    {
        return metres is >= Constants.Surfaces.MinMetres and <= Constants.Surfaces.MaxMetres;
    }
}
