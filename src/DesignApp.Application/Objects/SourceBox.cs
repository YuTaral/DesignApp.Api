namespace DesignApp.Application.Objects;

/// <summary>
/// Where an object is in the reference photo given with a layout (the first photo of the same room, from another view),
/// for the image model to copy it from there: shares of that photo, 0 at the left and top, 1 at the right and bottom.
/// </summary>
/// <param name="Left">The box's left edge.</param>
/// <param name="Top">The box's top edge.</param>
/// <param name="Right">The box's right edge.</param>
/// <param name="Bottom">The box's bottom edge.</param>
public sealed record SourceBox(double Left, double Top, double Right, double Bottom);
