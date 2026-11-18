using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.Objects;

/// <summary>
/// An object the user can put in a built room, with its usual size and color: one of the common objects of a room
/// type, or one the AI made from the user's description. A catalog object comes in variations (shapes and sizes).
/// </summary>
/// <param name="Key">A short id, unique within the room type (such as <c>sofa</c>).</param>
/// <param name="Label">Its name, such as "three-seat sofa".</param>
/// <param name="Shape">How it is drawn in 3D.</param>
/// <param name="Width">Its usual width, in centimetres.</param>
/// <param name="Depth">Its usual depth, in centimetres.</param>
/// <param name="Height">Its usual height, in centimetres.</param>
/// <param name="Color">Its usual color.</param>
/// <param name="Elevation">How high its bottom usually is above the floor, in centimetres (a window's sill; else 0).</param>
/// <param name="Form">Its outline from above.</param>
/// <param name="Variants">
/// Its variations, the first being the object itself (the size and outline above); <c>null</c> for an object the AI
/// described, which has none.
/// </param>
/// <param name="Decorations">The most popular small things to put on it (up to five), offered when it is added; none for most rugs and lamps.</param>
/// <param name="Materials">What it is usually made of or covered in, to choose from (such as "linen" or "velvet"); none for an object the AI described.</param>
/// <param name="Material">The material it starts in, one of <paramref name="Materials"/>; empty when it has none.</param>
/// <param name="Dressing">The decorations it has in a furnished new room, some of <paramref name="Decorations"/>; none when missing.</param>
public sealed record CatalogObject(string Key, string Label, ItemShape Shape, double Width, double Depth, double Height, HexColor Color, double Elevation = 0,
    ItemForm Form = ItemForm.Standard, IReadOnlyList<CatalogVariant>? Variants = null, IReadOnlyList<string>? Decorations = null,
    IReadOnlyList<string>? Materials = null, string Material = "", IReadOnlyList<string>? Dressing = null);
