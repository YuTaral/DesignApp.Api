namespace DesignApp.Application.Objects;

/// <summary>A spot in the image, as fractions (0 to 1) of its width and height from the top-left corner.</summary>
/// <param name="X">Across.</param>
/// <param name="Y">Down.</param>
public sealed record ImagePoint(double X, double Y);
