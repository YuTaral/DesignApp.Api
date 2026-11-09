using DesignApp.Domain.Exceptions;


namespace DesignApp.Domain.Objects;

/// <summary>A box in normalized image coordinates (0..1), so it is independent of photo resolution.</summary>
public readonly record struct BoundingBox
{
    /// <summary>Left edge, from 0 to 1.</summary>
    public double X { get; }
    /// <summary>Top edge, from 0 to 1.</summary>
    public double Y { get; }
    /// <summary>Width as a fraction of the photo's width.</summary>
    public double Width { get; }
    /// <summary>Height as a fraction of the photo's height.</summary>
    public double Height { get; }

    /// <summary>Creates a box that must lie inside the image.</summary>
    /// <param name="x">Left edge, from 0 to 1.</param>
    /// <param name="y">Top edge, from 0 to 1.</param>
    /// <param name="width">Width, greater than 0.</param>
    /// <param name="height">Height, greater than 0.</param>
    /// <exception cref="DomainException">The box is empty or reaches outside the image.</exception>
    public BoundingBox(double x, double y, double width, double height)
    {
        if (x < 0 || y < 0 || width <= 0 || height <= 0 || x + width > 1 + Constants.Boxes.Tolerance || y + height > 1 + Constants.Boxes.Tolerance)
            throw new DomainException(Constants.Messages.BoxOutside(x, y, width, height));
        (X, Y, Width, Height) = (x, y, width, height);
    }
}
