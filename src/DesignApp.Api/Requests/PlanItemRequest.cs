using static DesignApp.Domain.Constants;

namespace DesignApp.Api.Requests;

/// <summary>
/// One object of a built room, as sent and as returned. A window or door is on a <c>wall</c> instead of the floor: its
/// <c>x</c> is its middle along that wall, from the wall's left end as seen from inside the room facing it, <c>y</c> is 0,
/// and <c>elevation</c> is how high its bottom is above the floor.
/// </summary>
/// <param name="Label">What it is, such as "sofa".</param>
/// <param name="Shape">How it is drawn in 3D.</param>
/// <param name="X">Its middle across the room, in centimetres from the back-left corner seen from above.</param>
/// <param name="Y">Its middle along the room, in centimetres.</param>
/// <param name="Width">Its width, in centimetres.</param>
/// <param name="Depth">Its depth, in centimetres.</param>
/// <param name="Height">Its height, in centimetres.</param>
/// <param name="Rotation">Its turn, clockwise from above: 0, 90, 180 or 270 degrees.</param>
/// <param name="Color">Its color as #RRGGBB.</param>
/// <param name="Wall">The number of the wall a window or door is on (a rectangle's are 0 back, 1 right, 2 front, 3 left); null for other objects.</param>
/// <param name="Elevation">A window's height above the floor, in centimetres; 0 for other objects.</param>
/// <param name="Form">Its outline from above: Standard (a rectangle), LShaped (sofas, tables, boxes) or Round (tables, rugs).</param>
/// <param name="Parts">An L- or P-shaped object's parts (the usual ones when missing); null for other outlines.</param>
/// <param name="Decorations">The small things on it, such as "cushions", shown on it in the photo; none when missing.</param>
public sealed record PlanItemRequest(string? Label, ItemShape Shape, double X, double Y, double Width, double Depth, double Height, int Rotation, string? Color,
    int? Wall = null, double Elevation = 0, ItemForm Form = ItemForm.Standard, ItemPartsRequest? Parts = null, IReadOnlyList<string>? Decorations = null);
