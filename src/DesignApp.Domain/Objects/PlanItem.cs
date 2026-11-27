using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Extensions;

using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Objects;

/// <summary>
/// One object placed in a room the user builds: what it is, where it stands on the floor plan, how big it is, and its
/// color. Positions are the object's middle, in centimetres from the room's top-left corner as seen from above.
/// A window or door is in a wall instead, and a picture or wall box hangs on one: <see cref="X"/> is its middle along that
/// wall, from the wall's left end as seen from inside the room facing it, <see cref="Elevation"/> is how high its bottom
/// is above the floor, and <see cref="Depth"/> is its frame's thickness, or how far a hung object sticks out.
/// </summary>
public sealed record PlanItem
{
    /// <summary>What it is, such as "sofa".</summary>
    public string Label { get; }

    /// <summary>How it is drawn in 3D.</summary>
    public ItemShape Shape { get; }

    /// <summary>Its middle, across the room, in centimetres.</summary>
    public double X { get; }

    /// <summary>Its middle, along the room, in centimetres.</summary>
    public double Y { get; }

    /// <summary>Its width, in centimetres (before turning).</summary>
    public double Width { get; }

    /// <summary>Its depth, in centimetres (before turning).</summary>
    public double Depth { get; }

    /// <summary>Its height, in centimetres.</summary>
    public double Height { get; }

    /// <summary>How far it is turned, clockwise from above: 0, 90, 180 or 270 degrees.</summary>
    public int Rotation { get; }

    /// <summary>Its color.</summary>
    public HexColor Color { get; }

    /// <summary>The number of the wall a window, door or hung object is on (see <see cref="RoomOutline"/>); <c>null</c> for an object standing on the floor.</summary>
    public int? Wall { get; }

    /// <summary>How high its bottom is above the floor, in centimetres (a window's sill or a hung object's bottom; 0 for everything else).</summary>
    public double Elevation { get; }

    /// <summary>Its outline seen from above: a rectangle, an L or an ellipse in its width × depth.</summary>
    public ItemForm Form { get; }

    /// <summary>The sizes of an L- or P-shaped object's parts (its back run, corner leg and a P's chaise); <c>null</c> for other outlines.</summary>
    public ItemParts? Parts { get; }

    /// <summary>The small things the user put on it (cushions, a vase...), shown on it in the photo.</summary>
    public ItemDecorations Decorations { get; }

    /// <summary>What it is made of or covered in, such as "velvet" or "oak"; empty when only its color is given.</summary>
    public string Material { get; }

    /// <summary>A window's curtains or blinds; <c>null</c> for none, and for other objects.</summary>
    public WindowCovering? Covering { get; }

    /// <summary>A kitchen unit's worktop and backsplash; <c>null</c> for none, and for other objects.</summary>
    public Worktop? Worktop { get; }

    /// <summary>
    /// Whether a light (a lamp, a ceiling light, or a piece with a lamp on it) is switched on; <c>null</c> to follow the
    /// time of day (on in the evening). See <see cref="Extensions.Lights"/>.
    /// </summary>
    public bool? Lit { get; }

    /// <summary>Creates an object.</summary>
    /// <param name="label">What it is.</param>
    /// <param name="shape">How it is drawn.</param>
    /// <param name="x">Its middle, across the room, in centimetres.</param>
    /// <param name="y">Its middle, along the room, in centimetres.</param>
    /// <param name="width">Its width, in centimetres.</param>
    /// <param name="depth">Its depth, in centimetres.</param>
    /// <param name="height">Its height, in centimetres.</param>
    /// <param name="rotation">Its turn: 0, 90, 180 or 270 degrees.</param>
    /// <param name="color">Its color.</param>
    /// <param name="wall">The number of the wall a window, door or hung object is on; <c>null</c> for other objects.</param>
    /// <param name="elevation">A window's or hung object's height above the floor, in centimetres; 0 for other objects.</param>
    /// <param name="form">Its outline from above: L-shaped for sofas, tables and boxes, P-shaped for sofas, round for tables and rugs.</param>
    /// <param name="parts">An L- or P-shaped object's parts (the usual ones when missing); <c>null</c> for other outlines.</param>
    /// <param name="decorations">The small things on it, such as cushions; none when missing.</param>
    /// <param name="material">What it is made of, such as "velvet"; none when missing or empty.</param>
    /// <param name="covering">A window's curtains or blinds; <c>null</c> for none, and for other objects.</param>
    /// <param name="worktop">A kitchen unit's worktop and backsplash; <c>null</c> for none, and for other objects.</param>
    /// <param name="lit">Whether a light is switched on; <c>null</c> to follow the time of day.</param>
    /// <exception cref="DomainException">The name, a side, the position, the shape, the turn, the wall, the outline, a part or the material is invalid.</exception>
    public PlanItem(string label, ItemShape shape, double x, double y, double width, double depth, double height, int rotation, HexColor color,
        int? wall = null, double elevation = 0, ItemForm form = ItemForm.Standard, ItemParts? parts = null, ItemDecorations? decorations = null,
        string? material = null, WindowCovering? covering = null, Worktop? worktop = null, bool? lit = null)
    {
        var made = (material ?? "").Trim();

        var fits = form switch
        {
            ItemForm.Standard => true,
            ItemForm.LShaped => shape is ItemShape.Sofa or ItemShape.Table or ItemShape.Box,
            ItemForm.PShaped => shape is ItemShape.Sofa,
            ItemForm.Round => shape is ItemShape.Table or ItemShape.Rug,
            ItemForm.Arched => shape is ItemShape.Window or ItemShape.Archway or ItemShape.Niche,
            ItemForm.GlassScreen or ItemForm.GlassCubicle => shape is ItemShape.Box,
            ItemForm.Swivel => shape is ItemShape.Chair,
            _ => false,
        };

        if (!fits)
        {
            throw new DomainException(Constants.Messages.PlanItemForm);
        }

        if (made.Length > Constants.Plans.MaxMaterialLength)
        {
            throw new DomainException(Constants.Messages.PlanItemMaterial);
        }

        if (string.IsNullOrWhiteSpace(label) || label.Trim().Length > Constants.Plans.MaxLabelLength)
        {
            throw new DomainException(Constants.Messages.PlanItemLabel);
        }

        if (!new[] { width, depth, height }.All(side => side is >= Constants.Plans.MinItemCentimetres and <= Constants.Plans.MaxItemCentimetres))
        {
            throw new DomainException(Constants.Messages.PlanItemSize);
        }

        if (!double.IsFinite(x) || !double.IsFinite(y) || !Enum.IsDefined(shape) || rotation % Constants.Plans.RotationStep != 0 || rotation is < 0 or >= 360)
        {
            throw new DomainException(Constants.Messages.PlanItemPlacement);
        }

        if (shape.IsOnWall() != wall.HasValue || wall < 0)
        {
            throw new DomainException(Constants.Messages.PlanWallPlacement);
        }

        var raised = shape is ItemShape.Window or ItemShape.WallZone || shape.IsMounted();

        if (!double.IsFinite(elevation) || elevation < 0 || elevation > Constants.Plans.MaxItemCentimetres || (!raised && elevation != 0))
        {
            throw new DomainException(Constants.Messages.PlanItemPlacement);
        }

        if (shape.IsMounted() && depth > Constants.Plans.MaxMountedDepth)
        {
            throw new DomainException(Constants.Messages.PlanMountedDepth);
        }

        if (shape.IsZone() && (!IsZoneMaterial(shape, made) || decorations is { IsEmpty: false }))
        {
            throw new DomainException(Constants.Messages.PlanZoneMaterial);
        }

        if (shape.IsCeilingLight() && (wall.HasValue || elevation != 0 || decorations is { IsEmpty: false }))
        {
            throw new DomainException(Constants.Messages.PlanCeilingLight);
        }

        if (shape is ItemShape.Archway && decorations is { IsEmpty: false })
        {
            throw new DomainException(Constants.Messages.PlanArchway);
        }

        if (covering is not null && shape is not ItemShape.Window)
        {
            throw new DomainException(Constants.Messages.PlanCovering);
        }

        if (worktop is not null && shape is not ItemShape.Box)
        {
            throw new DomainException(Constants.Messages.PlanWorktop);
        }

        var hasParts = form is ItemForm.LShaped or ItemForm.PShaped;

        (Label, Shape, X, Y, Width, Depth, Height, Rotation, Color) = (label.Trim(), shape, x, y, width, depth, height, rotation, color);

        if (!hasParts && parts is not null)
        {
            throw new DomainException(Constants.Messages.PlanItemParts);
        }

        if (hasParts)
        {
            parts ??= ItemParts.Default(form, width, depth);
            parts.Check(form, width, depth);
        }

        (Wall, Elevation, Form, Parts, Decorations, Material) = (wall, elevation, form, parts, decorations ?? ItemDecorations.None, made);
        (Covering, Worktop, Lit) = (covering, worktop, lit);
    }

    /// <summary>Whether a zone's material is one of its kind's: a floor type for a floor area, a wall finish for a wall area.</summary>
    /// <param name="shape">The zone's shape.</param>
    /// <param name="material">Its material's name.</param>
    private static bool IsZoneMaterial(ItemShape shape, string material)
    {
        return shape is ItemShape.FloorZone
            ? Enum.TryParse<FloorType>(material, ignoreCase: false, out var floor) && Enum.IsDefined(floor)
            : Enum.TryParse<WallFinish>(material, ignoreCase: false, out var finish) && Enum.IsDefined(finish);
    }
}
