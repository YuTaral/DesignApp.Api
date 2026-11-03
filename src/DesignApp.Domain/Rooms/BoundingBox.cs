using DesignApp.Domain.Common;

namespace DesignApp.Domain.Rooms;

/// <summary>A box in normalized image coordinates (0..1), so it is independent of photo resolution.</summary>
public readonly record struct BoundingBox
{
    private const double Tolerance = 1e-4;

    public double X { get; }
    public double Y { get; }
    public double Width { get; }
    public double Height { get; }

    public BoundingBox(double x, double y, double width, double height)
    {
        if (x < 0 || y < 0 || width <= 0 || height <= 0 || x + width > 1 + Tolerance || y + height > 1 + Tolerance)
            throw new DomainException($"Bounding box ({x}, {y}, {width}, {height}) is outside the image.");
        (X, Y, Width, Height) = (x, y, width, height);
    }
}
