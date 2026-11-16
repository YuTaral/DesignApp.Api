namespace DesignApp.Application.Objects;

/// <summary>
/// Where one object of a built room appears in its 3D view, as the browser drew it: the object's position in the plan's
/// list, and its bounding box as shares of the picture (0 at the left and top, 1 at the right and bottom).
/// </summary>
/// <param name="Item">The object's position in the plan's objects.</param>
/// <param name="Left">The box's left edge.</param>
/// <param name="Top">The box's top edge.</param>
/// <param name="Right">The box's right edge.</param>
/// <param name="Bottom">The box's bottom edge.</param>
public sealed record ItemBox(int Item, double Left, double Top, double Right, double Bottom);
