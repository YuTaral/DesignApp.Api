namespace DesignApp.Api.Requests;

/// <summary>A corner of a built room's outline, as sent and as returned, in centimetres.</summary>
/// <param name="X">Across the plan, from the left.</param>
/// <param name="Y">Down the plan, from the top.</param>
public sealed record OutlinePointRequest(double X, double Y);
