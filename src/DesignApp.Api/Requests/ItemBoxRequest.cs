namespace DesignApp.Api.Requests;

/// <summary>
/// One object's box in the 3D view sent with <c>POST /api/plans/{id}/generate</c> (the <c>boxes</c> form field is a JSON
/// list of them): which object, and its edges as shares of the picture.
/// </summary>
/// <param name="Item">The object's position in the plan's objects.</param>
/// <param name="Left">The box's left edge, 0 to 1.</param>
/// <param name="Top">The box's top edge, 0 to 1.</param>
/// <param name="Right">The box's right edge, 0 to 1.</param>
/// <param name="Bottom">The box's bottom edge, 0 to 1.</param>
public sealed record ItemBoxRequest(int Item, double Left, double Top, double Right, double Bottom);
