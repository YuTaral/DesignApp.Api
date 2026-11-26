using DesignApp.Application.Objects;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;
using SL = DesignApp.Application.Constants.Builder.Layout;

namespace DesignApp.Application.Helpers;

/// <summary>
/// A room being furnished (a new room's starting layout, or a plan read from a photo): what is placed so far, the
/// floor it takes, and the windows, doors and hung things each wall has, so a new object can be checked before it goes in.
/// </summary>
/// <param name="Width">The width, in centimetres.</param>
/// <param name="Length">The length, in centimetres.</param>
/// <param name="Height">The ceiling height, in centimetres.</param>
/// <param name="Catalog">The objects that can be placed.</param>
public sealed record LayoutRoom(double Width, double Length, double Height, IReadOnlyList<CatalogObject> Catalog)
{
    /// <summary>
    /// A value kept within a range, like <see cref="Math.Clamp(double, double, double)"/>, but the range's middle when the
    /// room is too small for the range to exist (a lowest value above the highest), rather than an error: whatever is
    /// placed there is then turned down for not fitting.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="low">The lowest value wanted.</param>
    /// <param name="high">The highest value wanted.</param>
    public static double Within(double value, double low, double high)
    {
        return low > high ? (low + high) / 2 : Math.Clamp(value, low, high);
    }

    private readonly List<(double X1, double Y1, double X2, double Y2, double Top)> _taken = [];
    private readonly List<(PlanWall Wall, double From, double To, double Bottom, double Top)> _openings = [];
    private readonly List<(PlanWall Wall, double From, double To, double Bottom, double Top)> _hung = [];

    /// <summary>The objects placed so far.</summary>
    public List<PlanItem> Items { get; } = [];

    /// <summary>How much of the left wall, from the back, is free of the door and its swing.</summary>
    public double FreeAlongLeft { get; private set; }

    /// <summary>A catalog object by its key.</summary>
    /// <param name="key">The key.</param>
    public CatalogObject? Get(string key)
    {
        return Catalog.FirstOrDefault(o => o.Key == key);
    }

    /// <summary>An object's size with another width.</summary>
    /// <param name="key">The key.</param>
    /// <param name="width">The width wanted.</param>
    public (double Width, double Depth) Size(string key, double width)
    {
        return (Math.Round(width), Get(key)?.Depth ?? 0);
    }

    /// <summary>The door: near the front of the left wall, with the floor it swings over kept free.</summary>
    public void Door()
    {
        var door = Catalog.FirstOrDefault(o => o.Shape == ItemShape.Door);

        FreeAlongLeft = Length;

        if (door is null || Length < door.Width + 2 * SL.DoorMargin)
        {
            return;
        }

        var height = Math.Min(door.Height, Height - 20);
        // Along the left wall, its left end (facing it from inside) is the front corner.
        var along = SL.DoorMargin + door.Width / 2;
        var (from, to) = (Length - SL.DoorMargin - door.Width, Length - SL.DoorMargin);

        Items.Add(new PlanItem(door.Label, door.Shape, along, 0, door.Width, door.Depth, height, 0, door.Color, (int)PlanWall.Left, material: door.Material));
        _openings.Add((PlanWall.Left, from, to, 0, height));
        _taken.Add((0, from, door.Width, to, height));
        FreeAlongLeft = from - SL.SideGap;
    }

    /// <summary>
    /// The windows: on the back wall, evenly spaced, with glass of a share of the floor area; what the back wall can't
    /// hold goes in one window on the right wall.
    /// </summary>
    /// <param name="sill">How high their bottom is above the floor, in centimetres.</param>
    /// <param name="share">The glass wanted, as a share of the floor area.</param>
    /// <param name="dressing">What they are dressed with, such as sheer curtains.</param>
    public void Windows(double sill, double share, WindowCovering dressing)
    {
        var window = Catalog.FirstOrDefault(o => o.Key == "window") ?? Catalog.FirstOrDefault(o => o.Shape == ItemShape.Window);

        if (window is null)
        {
            return;
        }

        var height = Math.Min(SL.WindowHeight, Height - sill - SL.WindowHead);

        if (height < 60)
        {
            return;
        }

        var wanted = Width * Length * share / height;
        var count = Math.Max(1, (int)Math.Ceiling(wanted / SL.MaxWindowWidth));
        var each = Math.Clamp(wanted / count, SL.MinWindowWidth, SL.MaxWindowWidth);

        while (count > 1 && count * each + (count + 1) * SL.WindowGap > Width)
        {
            count--;
        }

        each = Math.Min(each, Width - 2 * SL.WindowGap);

        if (each < SL.MinWindowWidth)
        {
            return;
        }

        each = Math.Round(each / 10) * 10;
        var step = Width / count;

        for (var i = 0; i < count; i++)
        {
            AddWindow(window, PlanWall.Back, step * (i + 0.5), each, height, sill, dressing);
        }

        var missing = wanted - count * each;

        if (missing >= SL.MinWindowWidth && Length >= missing + 2 * SL.WindowGap)
        {
            AddWindow(window, PlanWall.Right, Length * 0.35, Math.Round(Math.Min(missing, SL.MaxWindowWidth) / 10) * 10, height, sill, dressing);
        }
    }

    /// <summary>Adds a window.</summary>
    /// <param name="window">The catalog window.</param>
    /// <param name="wall">Its wall.</param>
    /// <param name="along">Its middle along the wall, from its left end facing it (the back wall's is the left corner, the right wall's the back corner).</param>
    /// <param name="width">Its width.</param>
    /// <param name="height">Its height.</param>
    /// <param name="sill">Its height above the floor.</param>
    /// <param name="dressing">What it is dressed with.</param>
    private void AddWindow(CatalogObject window, PlanWall wall, double along, double width, double height, double sill, WindowCovering dressing)
    {
        Items.Add(new PlanItem(window.Label, window.Shape, Math.Round(along), 0, width, window.Depth, Math.Round(height), 0, window.Color, (int)wall, sill,
            material: window.Material, covering: dressing));
        _openings.Add((wall, along - width / 2, along + width / 2, sill, sill + height));
    }

    /// <summary>The first window on the back wall, if any.</summary>
    public PlanItem? BackWindow()
    {
        return Items.FirstOrDefault(i => i.Shape == ItemShape.Window && i.Wall == (int)PlanWall.Back);
    }

    /// <summary>Something hung under the first back window, such as a radiator, when it fits there.</summary>
    /// <param name="key">The object.</param>
    public PlanItem? UnderWindow(string key)
    {
        var window = BackWindow();

        return window is null ? null : Hang(key, PlanWall.Back, window.X);
    }

    /// <summary>
    /// Something hung on a wall (a picture, a TV, wall cabinets), above what stands against it: clear of the wall's
    /// windows and doors, of other hung things and of furniture taller than its bottom, inside the wall and under the ceiling.
    /// </summary>
    /// <param name="key">The object.</param>
    /// <param name="wall">The wall.</param>
    /// <param name="at">Its middle: across the plan for the back and front walls, down it for the sides.</param>
    /// <param name="width">Its width, when not the usual.</param>
    /// <param name="elevation">Its bottom above the floor, when not the usual.</param>
    /// <returns>The object hung, or <c>null</c> when it doesn't fit there.</returns>
    public PlanItem? Hang(string key, PlanWall wall, double at, double? width = null, double? elevation = null)
    {
        var o = Get(key);

        if (o is null)
        {
            return null;
        }

        var w = Math.Round(width ?? o.Width);
        var bottom = Math.Round(Math.Min(elevation ?? o.Elevation, Height - o.Height - SL.CeilingGap));
        var item = new PlanItem(o.Label, o.Shape, Math.Round(Along(wall, at)), 0, w, o.Depth, o.Height, 0, o.Color, (int)wall, bottom,
            decorations: Dress(o), material: o.Material);

        return HangItem(item);
    }

    /// <summary>
    /// A given thing hung where it says (its wall, its middle along it and its bottom), when it fits there: inside the
    /// wall and under the ceiling, clear of the wall's windows, doors and other hung things, and of furniture taller
    /// than its bottom.
    /// </summary>
    /// <param name="item">The hung thing (a picture or wall box on a wall).</param>
    /// <returns>The thing hung, or <c>null</c> when it doesn't fit there.</returns>
    public PlanItem? HangItem(PlanItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var (wall, length, from, to, bottom, top) = SpanOn(item);
        var inside = from >= 0 && to <= length && bottom >= 0 && top <= Height - SL.CeilingGap;
        var crossed = _openings.Concat(_hung).Any(x => x.Wall == wall && from < x.To && x.From < to && bottom < x.Top && x.Bottom < top);
        var standing = _taken.Any(t => Touches(t, wall) && from < Span(t, wall).To && Span(t, wall).From < to && t.Top > bottom);

        if (!inside || crossed || standing)
        {
            return null;
        }

        Items.Add(item);
        _hung.Add((wall, from, to, bottom, top));

        return item;
    }

    /// <summary>
    /// A given window or door where it says (its wall, its middle along it and its bottom), when it fits there: inside
    /// the wall, under the ceiling and clear of the wall's other windows and doors.
    /// </summary>
    /// <param name="item">The window or door.</param>
    /// <returns>The window or door, or <c>null</c> when it doesn't fit there.</returns>
    public PlanItem? AddOpening(PlanItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var (wall, length, from, to, bottom, top) = SpanOn(item);
        var inside = from >= 0 && to <= length && bottom >= 0 && top <= Height;
        var crossed = _openings.Any(x => x.Wall == wall && from < x.To && x.From < to && bottom < x.Top && x.Bottom < top);

        if (!inside || crossed)
        {
            return null;
        }

        Items.Add(item);
        _openings.Add((wall, from, to, bottom, top));

        return item;
    }

    /// <summary>
    /// Where a window, door or hung thing is on its wall: the wall and its length, the thing's span along it in plan
    /// coordinates (across for the back and front walls, down for the sides), and its bottom and top.
    /// </summary>
    /// <param name="item">The thing on a wall.</param>
    private (PlanWall Wall, double Length, double From, double To, double Bottom, double Top) SpanOn(PlanItem item)
    {
        var wall = (PlanWall)(item.Wall ?? 0);
        var at = Along(wall, item.X);

        return (wall, wall is PlanWall.Left or PlanWall.Right ? Length : Width, at - item.Width / 2, at + item.Width / 2, item.Elevation, item.Elevation + item.Height);
    }

    /// <summary>
    /// Where a point along a wall is in plan coordinates, or the other way round (the same on both): a wall's own
    /// "along" runs from its left end seen from inside, so the front wall's runs from the right and the left wall's
    /// from the front, while the plan's run from the left and the back.
    /// </summary>
    /// <param name="wall">The wall.</param>
    /// <param name="value">Along the wall, or across (back and front walls) or down (side walls) the plan.</param>
    private double Along(PlanWall wall, double value)
    {
        return wall switch
        {
            PlanWall.Front => Width - value,
            PlanWall.Left => Length - value,
            _ => value,
        };
    }

    /// <summary>Something hung at the first of some spots where it fits (art above a sideboard, else on another wall).</summary>
    /// <param name="key">The object.</param>
    /// <param name="spots">The walls and points to try, in order.</param>
    /// <returns>The object hung, or <c>null</c> when it fits at none.</returns>
    public PlanItem? HangAny(string key, params (PlanWall Wall, double At)[] spots)
    {
        foreach (var (wall, at) in spots)
        {
            if (Hang(key, wall, at) is { } hung)
            {
                return hung;
            }
        }

        return null;
    }

    /// <summary>
    /// Runs of an object filling the gaps along the back wall between what already stands against it (base cabinets
    /// between the sink, cooker and fridge), each gap at least <see cref="SL.MinRun"/> wide.
    /// </summary>
    /// <param name="key">The object.</param>
    /// <returns>The runs placed.</returns>
    public List<PlanItem> FillBack(string key)
    {
        var against = _taken.Where(t => Touches(t, PlanWall.Back)).OrderBy(t => t.X1).ToList();
        var runs = new List<PlanItem>();
        var cursor = 0.0;

        foreach (var t in against.Append((Width, 0, Width, 0, 0)))
        {
            if (Run(key, PlanWall.Back, cursor, t.X1) is { } run)
            {
                runs.Add(run);
            }

            cursor = Math.Max(cursor, t.X2);
        }

        return runs;
    }

    /// <summary>An object against a wall, facing into the room, as near a point along the wall as it fits.</summary>
    /// <param name="key">The object.</param>
    /// <param name="wall">The wall.</param>
    /// <param name="at">Where its middle should be: across the plan for the back and front walls, down it for the sides.</param>
    /// <param name="width">Its width, when not the usual.</param>
    /// <param name="depth">Its depth, when not the usual.</param>
    /// <param name="label">Its name, when not the usual.</param>
    /// <returns>The object placed, or <c>null</c> when it fits nowhere along the wall.</returns>
    public PlanItem? Against(string key, PlanWall wall, double at, double? width = null, double? depth = null, string? label = null)
    {
        var o = Get(key);

        if (o is null)
        {
            return null;
        }

        var (w, d) = (width ?? o.Width, depth ?? o.Depth);
        var sideways = wall is PlanWall.Left or PlanWall.Right;
        var length = sideways ? Length : Width;

        // Wider than the wall: it fits nowhere along it.
        if (w > length)
        {
            return null;
        }

        // Try the point first, then further and further either side of it.
        for (var shift = 0.0; shift <= length; shift += SL.Step)
        {
            foreach (var sign in shift == 0 ? new[] { 1 } : new[] { 1, -1 })
            {
                var point = Math.Clamp(at + sign * shift, w / 2, length - w / 2);
                var (x, y, turn) = wall switch
                {
                    PlanWall.Back => (point, d / 2, 0),
                    PlanWall.Front => (point, Length - d / 2, 180),
                    PlanWall.Left => (d / 2, point, 270),
                    _ => (Width - d / 2, point, 90),
                };

                if (Place(key, x, y, turn, w, d, label) is { } placed)
                {
                    return placed;
                }
            }
        }

        return null;
    }

    /// <summary>An object in a corner, against both walls.</summary>
    /// <param name="key">The object.</param>
    /// <param name="back">The back or front wall.</param>
    /// <param name="side">The left or right wall.</param>
    /// <returns>The object placed, or <c>null</c> when the corner is taken.</returns>
    public PlanItem? Corner(string key, PlanWall back, PlanWall side)
    {
        var o = Get(key);

        if (o is null)
        {
            return null;
        }

        var x = side == PlanWall.Left ? o.Width / 2 + 5 : Width - o.Width / 2 - 5;
        var y = back == PlanWall.Back ? o.Depth / 2 + 5 : Length - o.Depth / 2 - 5;

        return Place(key, x, y, back == PlanWall.Back ? 0 : 180);
    }

    /// <summary>A run of an object along a wall between two points, as wide as the gap (cabinets filling a worktop).</summary>
    /// <param name="key">The object.</param>
    /// <param name="wall">The back wall.</param>
    /// <param name="from">Where the run starts, across the plan.</param>
    /// <param name="to">Where it ends.</param>
    /// <returns>The run placed, or <c>null</c> when the gap is too small or taken.</returns>
    public PlanItem? Run(string key, PlanWall wall, double from, double to)
    {
        return to - from >= SL.MinRun ? Against(key, wall, (from + to) / 2, Math.Round(to - from)) : null;
    }

    /// <summary>A rug: flat under the furniture, kept inside the room.</summary>
    /// <param name="key">The rug.</param>
    /// <param name="x">Its middle across the plan.</param>
    /// <param name="y">Its middle down the plan.</param>
    /// <param name="across">Its size across.</param>
    /// <param name="down">Its size down.</param>
    public void Rug(string key, double x, double y, double across, double down)
    {
        if (across < SL.MinRug || down < SL.MinRug)
        {
            return;
        }

        Place(key, Within(x, across / 2, Width - across / 2), Within(y, down / 2, Length - down / 2), 0, Math.Round(across), Math.Round(down));
    }

    /// <summary>Chairs round a table, facing it: along both long sides, and one at each end when there is room.</summary>
    /// <param name="key">The chair.</param>
    /// <param name="x">The table's middle across the plan.</param>
    /// <param name="y">Its middle down the plan.</param>
    /// <param name="length">Its length.</param>
    /// <param name="depth">Its depth.</param>
    /// <param name="rotation">Its turn: 0 when its length runs across the plan, 90 when down.</param>
    public void Chairs(string key, double x, double y, double length, double depth, int rotation)
    {
        var chair = Get(key);

        if (chair is null)
        {
            return;
        }

        var perSide = Math.Max(1, (int)(length / SL.PlaceWidth));
        var reach = depth / 2 + chair.Depth / 2;
        var end = length / 2 + chair.Depth / 2;

        for (var i = 0; i < perSide; i++)
        {
            var offset = -length / 2 + length / perSide * (i + 0.5);

            if (rotation == 0)
            {
                Place(key, x + offset, y - reach, 0);
                Place(key, x + offset, y + reach, 180);
            }
            else
            {
                Place(key, x - reach, y + offset, 270);
                Place(key, x + reach, y + offset, 90);
            }
        }

        if (rotation == 0)
        {
            Place(key, x - end, y, 270);
            Place(key, x + end, y, 90);
        }
        else
        {
            Place(key, x, y - end, 0);
            Place(key, x, y + end, 180);
        }
    }

    /// <summary>
    /// An object at a point, if it fits: inside the walls, on free floor (rugs may lie under things), and not in front
    /// of a door, or of a window it would stand taller than.
    /// </summary>
    /// <param name="key">The object.</param>
    /// <param name="x">Its middle across the plan.</param>
    /// <param name="y">Its middle down the plan.</param>
    /// <param name="rotation">Its turn: 0 faces the front, 90 the left, 180 the back, 270 the right.</param>
    /// <param name="width">Its width, when not the usual.</param>
    /// <param name="depth">Its depth, when not the usual.</param>
    /// <param name="label">Its name, when not the usual.</param>
    /// <returns>The object placed, or <c>null</c> when it doesn't fit there.</returns>
    public PlanItem? Place(string key, double x, double y, int rotation, double? width = null, double? depth = null, string? label = null)
    {
        var o = Get(key);

        if (o is null)
        {
            return null;
        }

        var (w, d) = (Math.Round(width ?? o.Width), Math.Round(depth ?? o.Depth));
        var h = Math.Min(o.Height, Height - 5);
        var item = new PlanItem(label ?? o.Label, o.Shape, Math.Round(x), Math.Round(y), w, d, h, rotation, o.Color, decorations: Dress(o), material: o.Material,
            worktop: StartingWorktop(o));

        return Add(item);
    }

    /// <summary>
    /// A light on the ceiling at a point of the plan, over whatever stands below (it takes no floor), reaching no lower than
    /// <see cref="SL.LightClearance"/> above the floor.
    /// </summary>
    /// <param name="key">The catalog object.</param>
    /// <param name="x">Its middle across the plan.</param>
    /// <param name="y">Its middle down the plan.</param>
    /// <param name="width">Its width, when not the usual (spotlights spread over it).</param>
    /// <param name="depth">Its depth, when not the usual.</param>
    /// <returns>The light, or <c>null</c> when the catalog has no such object.</returns>
    public PlanItem? Light(string key, double x, double y, double? width = null, double? depth = null)
    {
        var item = Get(key) is { } o ? LightFrom(o, x, y, Height, width, depth) : null;

        if (item is not null)
        {
            Items.Add(item);
        }

        return item;
    }

    /// <summary>A ceiling light from its catalog object, at a point of a room with a ceiling so high.</summary>
    /// <param name="o">The catalog object.</param>
    /// <param name="x">Its middle across the plan.</param>
    /// <param name="y">Its middle down the plan.</param>
    /// <param name="ceiling">The ceiling's height, in centimetres.</param>
    /// <param name="width">Its width, when not the usual.</param>
    /// <param name="depth">Its depth, when not the usual.</param>
    public static PlanItem LightFrom(CatalogObject o, double x, double y, double ceiling, double? width = null, double? depth = null)
    {
        ArgumentNullException.ThrowIfNull(o);
        var drop = Math.Max(Domain.Constants.Plans.MinItemCentimetres, Math.Min(o.Height, ceiling - SL.LightClearance));

        var min = Domain.Constants.Plans.MinItemCentimetres;

        // Spotlights spread over a room too small for a margin are one spotlight.
        return new PlanItem(o.Label, o.Shape, Math.Round(x), Math.Round(y), Math.Max(min, Math.Round(width ?? o.Width)), Math.Max(min, Math.Round(depth ?? o.Depth)), drop, 0, o.Color,
            material: o.Material);
    }

    /// <summary>
    /// The worktop a kitchen unit starts with: white quartz, with a low upstand along a wall (an island has none); none
    /// for other objects.
    /// </summary>
    /// <param name="o">The catalog object.</param>
    private static Worktop? StartingWorktop(CatalogObject o)
    {
        var white = HexColor.Parse(SL.WorktopColor);

        return o.Worktop is { } spot && o.Shape is ItemShape.Box
            ? new Worktop(SL.WorktopMaterial, white, spot is WorktopSpot.Island ? BacksplashKind.None : BacksplashKind.Upstand, white)
            : null;
    }

    /// <summary>Keeps a square of floor free of standing things (rugs may still lie there), such as where the camera stands.</summary>
    /// <param name="x">Its middle across the plan.</param>
    /// <param name="y">Its middle down the plan.</param>
    /// <param name="size">Its side.</param>
    public void Reserve(double x, double y, double size)
    {
        _taken.Add((x - size / 2, y - size / 2, x + size / 2, y + size / 2, Height));
    }

    /// <summary>
    /// A given object standing on the floor where it says, when it fits there: inside the walls, on free floor (rugs may
    /// lie under things), and not in front of a door, or of a window or hung thing it would stand taller than.
    /// </summary>
    /// <param name="item">The object on the floor.</param>
    /// <returns>The object, or <c>null</c> when it doesn't fit there.</returns>
    public PlanItem? Add(PlanItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var turned = item.Rotation % 180 != 0;
        var (across, down) = turned ? (item.Depth, item.Width) : (item.Width, item.Depth);
        var box = (X1: item.X - across / 2, Y1: item.Y - down / 2, X2: item.X + across / 2, Y2: item.Y + down / 2, Top: item.Height);
        var rug = item.Shape == ItemShape.Rug;
        const double edge = 0.5;

        if (box.X1 < -edge || box.Y1 < -edge || box.X2 > Width + edge || box.Y2 > Length + edge)
        {
            return null;
        }

        if (!rug && _taken.Any(t => box.X1 < t.X2 - edge && t.X1 < box.X2 - edge && box.Y1 < t.Y2 - edge && t.Y1 < box.Y2 - edge))
        {
            return null;
        }

        if (!rug && _openings.Concat(_hung).Any(opening => Blocks(box, opening)))
        {
            return null;
        }

        Items.Add(item);

        if (!rug)
        {
            _taken.Add(box);
        }

        return item;
    }

    /// <summary>
    /// Whether an object against a wall stands in front of one of its windows or doors, or of something hung on it, and
    /// is taller than its bottom.
    /// </summary>
    /// <param name="box">The object's footprint and height.</param>
    /// <param name="opening">The window, door or hung thing: its wall, its span along it, its bottom and top.</param>
    private bool Blocks((double X1, double Y1, double X2, double Y2, double Top) box, (PlanWall Wall, double From, double To, double Bottom, double Top) opening)
    {
        var (from, to) = Span(box, opening.Wall);

        return Touches(box, opening.Wall) && from < opening.To && opening.From < to && box.Top > opening.Bottom;
    }

    /// <summary>Whether a footprint stands against a wall.</summary>
    /// <param name="box">The footprint.</param>
    /// <param name="wall">The wall.</param>
    private bool Touches((double X1, double Y1, double X2, double Y2, double Top) box, PlanWall wall)
    {
        return wall switch
        {
            PlanWall.Back => box.Y1 < SL.WallTouch,
            PlanWall.Front => box.Y2 > Length - SL.WallTouch,
            PlanWall.Left => box.X1 < SL.WallTouch,
            _ => box.X2 > Width - SL.WallTouch,
        };
    }

    /// <summary>How far along a wall a footprint reaches, in plan coordinates: across for the back and front walls, down for the sides.</summary>
    /// <param name="box">The footprint.</param>
    /// <param name="wall">The wall.</param>
    private static (double From, double To) Span((double X1, double Y1, double X2, double Y2, double Top) box, PlanWall wall)
    {
        return wall is PlanWall.Back or PlanWall.Front ? (box.X1, box.X2) : (box.Y1, box.Y2);
    }

    /// <summary>The decorations an object starts with in a furnished room, if any.</summary>
    /// <param name="o">The catalog object.</param>
    private static ItemDecorations? Dress(CatalogObject o)
    {
        return o.Dressing is { Count: > 0 } names ? new ItemDecorations(names) : null;
    }
}
