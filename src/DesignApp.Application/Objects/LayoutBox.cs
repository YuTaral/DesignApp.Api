namespace DesignApp.Application.Objects;

/// <summary>
/// An object the image model must draw inside a box of the photo: what it is, and where (shares of the picture, 0 at the
/// left and top, 1 at the right and bottom).
/// </summary>
/// <param name="Label">A short id for it.</param>
/// <param name="Description">The object and how it looks, such as "warm grey (#8A8580) three-seat sofa".</param>
/// <param name="Left">The box's left edge.</param>
/// <param name="Top">The box's top edge.</param>
/// <param name="Right">The box's right edge.</param>
/// <param name="Bottom">The box's bottom edge.</param>
/// <param name="FromReference">
/// Where the same object is in the reference photo, to copy it from there (its look, materials and decorations);
/// <c>null</c> to draw it from its description alone.
/// </param>
public sealed record LayoutBox(string Label, string Description, double Left, double Top, double Right, double Bottom, SourceBox? FromReference = null);
