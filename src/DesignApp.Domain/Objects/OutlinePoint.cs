namespace DesignApp.Domain.Objects;

/// <summary>A corner of a room's outline on the floor plan, in centimetres: across from the left, down from the top.</summary>
/// <param name="X">Across the plan.</param>
/// <param name="Y">Down the plan.</param>
public sealed record OutlinePoint(double X, double Y);
