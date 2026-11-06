namespace DesignApp.Application.Analysis;

/// <summary>A point of a stroke.</summary>
/// <param name="X">0..1 of the photo's width.</param>
/// <param name="Y">0..1 of the photo's height.</param>
public readonly record struct StrokePoint(double X, double Y);
