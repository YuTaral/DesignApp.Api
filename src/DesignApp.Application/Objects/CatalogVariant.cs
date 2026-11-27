using static DesignApp.Domain.Constants;

namespace DesignApp.Application.Objects;

/// <summary>
/// One of the variations of a catalog object, such as the L-shaped corner sofa of the sofa: its own name, size and
/// outline, in the object's color. The first variation is the object itself.
/// </summary>
/// <param name="Key">A short id, unique within the object (such as <c>corner</c>).</param>
/// <param name="Name">What the choice is called, such as "L-shaped".</param>
/// <param name="Label">The object's name in this variation, such as "L-shaped corner sofa".</param>
/// <param name="Shape">How it is drawn in 3D.</param>
/// <param name="Width">Its width, in centimetres.</param>
/// <param name="Depth">Its depth, in centimetres.</param>
/// <param name="Height">Its height, in centimetres.</param>
/// <param name="Elevation">How high its bottom is above the floor, in centimetres (a window's sill; else 0).</param>
/// <param name="Form">Its outline from above.</param>
/// <param name="ToCeiling">Whether it reaches the ceiling (a column): added as tall as the room, whatever <paramref name="Height"/> says.</param>
public sealed record CatalogVariant(string Key, string Name, string Label, ItemShape Shape, double Width, double Depth, double Height, double Elevation, ItemForm Form,
    bool ToCeiling = false);
