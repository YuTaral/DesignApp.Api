using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Extensions;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;
using B = DesignApp.Application.Constants.Builder;
using C = DesignApp.Application.Constants.Builder.Camera;

namespace DesignApp.Application.Helpers;

/// <summary>
/// Writes the image model's instruction for a built room straight from its plan, so every number is exact: the room's
/// size as the photo sees it, the walls' and floor's colors and the floor's material, and each object with its color,
/// its distances from the left wall and the far wall, its size and which way it faces, then the windows and doors and
/// the things hung on the walls in view, leaving out whatever the 3D view's camera can't see (beside or behind it, or
/// behind an inner corner of a room that isn't a rectangle). It keeps the rest of the room empty. The style the user
/// chose and their own instructions come right after the opening.
/// </summary>
/// <remarks>
/// The photo looks at <see cref="RoomPlan.View"/> from straight across the room (<see cref="RoomOutline.Facing"/>), so
/// the plan's coordinates (centimetres from its top-left corner seen from above) are turned into the photo's: across
/// from its left wall (the room's farthest point to the left), and deep from its far wall. A rectangle's prompt names
/// its walls as before; another outline adds a sentence on the walls in view and names walls by where they are. From a
/// near corner (<see cref="RoomPlan.Corner"/>) the walls keep those names, and the camera sentence says which shows where.
/// </remarks>
public static partial class PlanPrompt
{
    /// <summary>The instruction for a plan.</summary>
    /// <param name="plan">The room.</param>
    /// <param name="style">The interior style of the photo.</param>
    /// <param name="instructions">The user's own extra instructions, if any.</param>
    /// <param name="shot">The view the photo is made from; the room's own (<see cref="RoomPlan.View"/> and <see cref="RoomPlan.Corner"/>) when missing.</param>
    /// <returns>The prompt.</returns>
    public static string Write(RoomPlan plan, PhotoStyle style = PhotoStyle.AsBuilt, string? instructions = null, PlanShot? shot = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var culture = CultureInfo.InvariantCulture;
        var (view, corner) = shot is null ? (plan.View, plan.Corner) : (shot.View, shot.Corner);
        var frame = new Frame(plan.Outline, view, corner);
        var text = new StringBuilder();
        var camera = new PlanCamera(frame.Across, frame.Deep, plan.Height * 100, frame.CameraFromLeft, corner);
        var standing = plan.Items
            .Where(i => i.Wall is null)
            .Select(item => (Item: item, Seen: Look(Footprint(item), frame, camera)))
            .Where(seen => seen.Seen.Edges is not null)
            .ToList();
        var openings = plan.Items
            .Where(i => i.Shape.IsOpening() && frame.Faces(i.Wall!.Value))
            .Select(item => (Item: item, Seen: Look(OnWallPoints(item, plan.Outline, 0), frame, camera)))
            .Where(seen => seen.Seen.Edges is not null)
            .ToList();
        var mounted = plan.Items
            .Where(i => i.Shape.IsMounted() && frame.Faces(i.Wall!.Value))
            .Select(item => (Item: item, Seen: Look(OnWallPoints(item, plan.Outline, item.Depth), frame, camera)))
            .Where(seen => seen.Seen.Edges is not null)
            .ToList();
        // Decorations are drawn only on objects the photo shows, so only those decide the sentence.
        var decorated = standing.Concat(openings).Concat(mounted).Any(seen => !seen.Item.Decorations.IsEmpty);

        text.Append(B.Prompt.Intro(Words(plan.RoomType.ToString())));

        // The style and the user's own words come early: at the end of a long prompt the image model ignored them.
        if (B.Prompt.Style(style) is { Length: > 0 } styled)
        {
            text.Append(culture, $" {styled}");
            text.Append(Kept(standing.Concat(mounted).Select(seen => seen.Item).ToList()));
        }

        if (instructions?.Trim().TrimEnd('.') is { Length: > 0 } extra)
        {
            text.Append(culture, $" {B.Prompt.Extra(extra)}");
        }

        text.Append(culture, $" {B.Prompt.Room(Cm(frame.Across), Cm(frame.Deep), Cm(plan.Height * 100))}");

        if (!plan.Outline.IsRectangle && Walls(frame, camera, plan.Height * 100) is { Length: > 0 } walls)
        {
            text.Append(culture, $" {B.Prompt.Shape(walls)}");
        }

        text.Append(culture, $" {B.Prompt.Surfaces(Color(plan.WallColor), B.Prompt.FloorWords(plan.FloorType), Color(plan.FloorColor))}");
        text.Append(culture, $" {(corner == ViewCorner.None ? B.Prompt.Camera : B.Prompt.CornerCamera(corner == ViewCorner.Left))}");
        text.Append(culture, $" {(decorated ? B.Prompt.WithDecorations : B.Prompt.Undecorated)}");

        if (standing.Count > 0)
        {
            text.Append(culture, $" {B.Prompt.ObjectsHeading}");
            text.Append(string.Join("; ", standing.Select((seen, n) => $"{n + 1}. {Standing(seen.Item, frame)}{Cut(seen.Seen)}{Decorations(seen.Item)}")));
            text.Append('.');
        }

        text.Append(' ');
        text.Append(openings.Count == 0
            ? B.Prompt.NoOpenings
            : $"{B.Prompt.OpeningsHeading}{string.Join("; ", openings.Select(seen => OnWall(seen.Item, frame) + Cut(seen.Seen) + Decorations(seen.Item)))}.");

        if (mounted.Count > 0)
        {
            text.Append(culture, $" {B.Prompt.MountedHeading}{string.Join("; ", mounted.Select(seen => Hung(seen.Item, frame) + Cut(seen.Seen) + Decorations(seen.Item)))}.");
        }

        if (standing.Count > 0 || mounted.Count > 0)
        {
            text.Append(culture, $" {B.Prompt.KeepColors}");
        }

        return text.ToString();
    }

    /// <summary>An object on the floor: its color, where its middle is, its size as the photo sees it, and its facing.</summary>
    /// <param name="item">The object.</param>
    /// <param name="frame">The photo's frame.</param>
    private static string Standing(PlanItem item, Frame frame)
    {
        var (fromLeft, fromFar) = frame.Point(item.X, item.Y);
        var turned = item.Rotation % 180 != 0;
        var (planAcross, planAlong) = turned ? (item.Depth, item.Width) : (item.Width, item.Depth);
        var (across, deep) = frame.Sideways ? (planAlong, planAcross) : (planAcross, planAlong);
        var facing = item.Shape is ItemShape.Rug or ItemShape.Cylinder or ItemShape.Plant or ItemShape.Lamp or ItemShape.Table
            ? ""
            : $", {frame.Facing(item.Rotation)}";
        var size = item.Shape is ItemShape.Rug
            ? B.Prompt.FlatSize(Cm(across), Cm(deep))
            : B.Prompt.Size(Cm(across), Cm(deep), Cm(item.Height));
        var appliance = IsAppliance(item) ? B.Prompt.Appliance : "";

        return B.Prompt.Standing(item.Label, Color(item.Color), Cm(fromLeft), Cm(fromFar), size) + Outline(item, frame) + appliance + facing;
    }

    /// <summary>Points over an object on the plan: a grid over its footprint, on the floor and at its top.</summary>
    /// <param name="item">The object on the floor.</param>
    private static IEnumerable<(double X, double Y, double Height)> Footprint(PlanItem item)
    {
        var turn = item.Rotation * Math.PI / 180;
        var (cos, sin) = (Math.Cos(turn), Math.Sin(turn));

        foreach (var x in PlanCamera.Steps(-item.Width / 2, item.Width / 2))
        {
            foreach (var y in PlanCamera.Steps(-item.Depth / 2, item.Depth / 2))
            {
                var (px, py) = (item.X + x * cos - y * sin, item.Y + x * sin + y * cos);

                yield return (px, py, 0);
                yield return (px, py, item.Height);
            }
        }
    }

    /// <summary>Points over a window, door or hung thing on the plan: a grid over its face, out from its wall.</summary>
    /// <param name="item">The window, door or hung thing.</param>
    /// <param name="outline">The room's walls.</param>
    /// <param name="out">How far its face is out from the wall, in centimetres.</param>
    private static IEnumerable<(double X, double Y, double Height)> OnWallPoints(PlanItem item, RoomOutline outline, double @out)
    {
        var wall = outline.Walls[item.Wall!.Value];

        foreach (var along in PlanCamera.Steps(item.X - item.Width / 2, item.X + item.Width / 2))
        {
            var point = wall.Point(along);

            foreach (var height in PlanCamera.Steps(item.Elevation, item.Elevation + item.Height))
            {
                yield return (point.X + wall.NormalX * @out, point.Y + wall.NormalY * @out, height);
            }
        }
    }

    /// <summary>
    /// How much of a thing the photo shows, from points on it on the plan: the picture's edges that cut it (null when it is
    /// out of view), and whether a wall hides part of it (only in a room that isn't a rectangle).
    /// </summary>
    /// <param name="points">Points on it: across and down the plan, and above the floor.</param>
    /// <param name="frame">The photo's frame.</param>
    /// <param name="camera">The camera.</param>
    private static Seen Look(IEnumerable<(double X, double Y, double Height)> points, Frame frame, PlanCamera camera)
    {
        var all = points.ToList();
        var open = frame.Outline.IsRectangle ? all : all.Where(p => frame.Outline.Sees(frame.Camera, new OutlinePoint(p.X, p.Y))).ToList();
        var edges = open.Count == 0 ? null : camera.Sees(open.Select(p => ToPhoto(frame, p)));

        return new Seen(edges, edges is not null && open.Count < all.Count);
    }

    /// <summary>A point of the plan, with its height, as the photo's distances.</summary>
    /// <param name="frame">The photo's frame.</param>
    /// <param name="point">Across and down the plan, and above the floor.</param>
    private static (double FromLeft, double FromFar, double Height) ToPhoto(Frame frame, (double X, double Y, double Height) point)
    {
        var (fromLeft, fromFar) = frame.Point(point.X, point.Y);

        return (fromLeft, fromFar, point.Height);
    }

    /// <summary>
    /// The walls of a room that isn't a rectangle as the photo sees them, besides the far wall: each wall facing the
    /// camera in front of it, and each side wall, with where it runs.
    /// </summary>
    /// <param name="frame">The photo's frame.</param>
    /// <param name="camera">The camera.</param>
    /// <param name="ceiling">The ceiling's height, in centimetres.</param>
    /// <returns>The walls in words, or nothing when none is in view.</returns>
    private static string Walls(Frame frame, PlanCamera camera, double ceiling)
    {
        var described = new List<string>();

        for (var index = 0; index < frame.Outline.Walls.Count; index++)
        {
            var wall = frame.Outline.Walls[index];

            if (index == frame.View || !frame.Faces(index))
            {
                continue;
            }

            var points = PlanCamera.Steps(0, wall.Length)
                .SelectMany(along => new[] { (wall.Point(along), 0.0), (wall.Point(along), ceiling) })
                .Select(p => (p.Item1.X, p.Item1.Y, p.Item2));

            if (Look(points, frame, camera).Edges is null)
            {
                continue;
            }

            var (startLeft, startFar) = frame.Point(wall.Start.X, wall.Start.Y);
            var (endLeft, endFar) = frame.Point(wall.End.X, wall.End.Y);

            described.Add(frame.Parallel(index)
                ? B.Prompt.StepWall(Cm(startFar), Cm(Math.Min(startLeft, endLeft)), Cm(Math.Max(startLeft, endLeft)))
                : B.Prompt.SideWall(frame.OnLeft(index), Cm(startLeft), Cm(Math.Min(startFar, endFar)), Cm(Math.Max(startFar, endFar))));
        }

        return string.Join("; ", described);
    }

    /// <summary>The decorations the user put on an object, as words after it, or none.</summary>
    /// <param name="item">The object.</param>
    private static string Decorations(PlanItem item)
    {
        var names = item.Decorations.Names;

        return names.Count == 0 ? "" : B.Prompt.Decorated(Listed(names));
    }

    /// <summary>
    /// What a style must leave alone in this room, by name: its appliances stay plain, and every other object keeps its
    /// color. Rugs and plants are left out (a style hardly changes them).
    /// </summary>
    /// <param name="inView">The objects on the floor the photo shows.</param>
    /// <returns>The sentences, each after a space, or nothing.</returns>
    private static string Kept(IReadOnlyList<PlanItem> inView)
    {
        var appliances = inView.Where(IsAppliance).Select(i => $"the {i.Label}").Distinct().ToList();
        var colored = inView
            .Where(i => !IsAppliance(i) && i.Shape is not (ItemShape.Rug or ItemShape.Plant))
            .Select(i => B.Prompt.KeepsColor(i.Shape is ItemShape.Picture ? B.Prompt.FrameOf(i.Label) : i.Label, ColorName(i.Color)))
            .Distinct()
            .ToList();
        var text = "";

        if (appliances.Count > 0)
        {
            text += $" {B.Prompt.PlainAppliances(Listed(appliances), appliances.Count)}";
        }

        if (colored.Count > 0)
        {
            text += $" {B.Prompt.KeptColors(Listed(colored))}";
        }

        return text;
    }

    /// <summary>Whether an object is an appliance (a fridge, a cooker...), which the style leaves plain.</summary>
    /// <param name="item">The object.</param>
    private static bool IsAppliance(PlanItem item)
    {
        return B.Prompt.ApplianceWords.Any(word => item.Label.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Names as words: "a", "a and b", "a, b and c".</summary>
    /// <param name="names">The names.</param>
    private static string Listed(IReadOnlyList<string> names)
    {
        return names.Count == 1 ? names[0] : $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";
    }

    /// <summary>The words for a thing the photo shows only part of, or none when it is all in view.</summary>
    /// <param name="seen">How much of it the photo shows.</param>
    private static string Cut(Seen seen)
    {
        var cut = seen.Edges is { Count: > 0 } edges ? B.Prompt.CutBy(edges) : "";

        return seen.Hidden ? cut + B.Prompt.PartlyHidden : cut;
    }

    /// <summary>
    /// An object's outline when it isn't a rectangle: an L or a P (how deep its runs are, where its leg's middle is, and
    /// a P's chaise) or an ellipse (round or oval).
    /// </summary>
    /// <param name="item">The object.</param>
    /// <param name="frame">The photo's frame.</param>
    private static string Outline(PlanItem item, Frame frame)
    {
        switch (item.Form)
        {
            case ItemForm.LShaped or ItemForm.PShaped:
            {
                // Before turning the back run is at -y and the leg at the right end, between the run and a P's chaise.
                var parts = item.Parts ?? ItemParts.Default(item.Form, item.Width, item.Depth);
                var (legLeft, legFar) = PartMiddle(item, frame, item.Width / 2 - parts.LegWidth / 2, (parts.RunDepth - parts.ChaiseDepth) / 2);

                if (item.Form == ItemForm.LShaped)
                {
                    return B.Prompt.LShaped(Cm(parts.RunDepth), Cm(parts.LegWidth), Cm(legLeft), Cm(legFar));
                }

                var (chaiseLeft, chaiseFar) = PartMiddle(item, frame, item.Width / 2 - parts.ChaiseWidth / 2, item.Depth / 2 - parts.ChaiseDepth / 2);

                return B.Prompt.PShaped(Cm(parts.RunDepth), Cm(parts.LegWidth), Cm(legLeft), Cm(legFar),
                    Cm(parts.ChaiseWidth), Cm(parts.ChaiseDepth), Cm(chaiseLeft), Cm(chaiseFar));
            }
            case ItemForm.Round:
            {
                return Math.Abs(item.Width - item.Depth) < 1 ? B.Prompt.RoundForm : B.Prompt.OvalForm;
            }
            default:
            {
                return "";
            }
        }
    }

    /// <summary>Where a part's middle is, as the photo sees it: from the object's middle before turning, turned clockwise.</summary>
    /// <param name="item">The object.</param>
    /// <param name="frame">The photo's frame.</param>
    /// <param name="x">The part's middle across the object before turning, from the object's middle.</param>
    /// <param name="y">The part's middle along the object before turning (towards its front), from the object's middle.</param>
    private static (double FromLeft, double FromFar) PartMiddle(PlanItem item, Frame frame, double x, double y)
    {
        var turn = item.Rotation * Math.PI / 180;
        var (dx, dy) = (x * Math.Cos(turn) - y * Math.Sin(turn), x * Math.Sin(turn) + y * Math.Cos(turn));

        return frame.Point(item.X + dx, item.Y + dy);
    }

    /// <summary>A window or door on a wall in view: which wall, where along it, its size and its height above the floor.</summary>
    /// <param name="item">The window or door.</param>
    /// <param name="frame">The photo's frame.</param>
    private static string OnWall(PlanItem item, Frame frame)
    {
        var where = frame.Place(item.Wall!.Value, item.X);

        return B.Prompt.Opening(item.Label, Color(item.Color), where, Cm(item.Width), Cm(item.Height), Cm(item.Elevation));
    }

    /// <summary>A thing hung on a wall in view: which wall, where along it, its size, its height above the floor and how far it sticks out.</summary>
    /// <param name="item">The hung thing.</param>
    /// <param name="frame">The photo's frame.</param>
    private static string Hung(PlanItem item, Frame frame)
    {
        var where = frame.Place(item.Wall!.Value, item.X);

        return B.Prompt.Mounted(Named(item), where, Cm(item.Width), Cm(item.Height), Cm(item.Elevation), Cm(item.Depth));
    }

    /// <summary>An object in a few words for the image model: its color and name, and its decorations, if any.</summary>
    /// <param name="item">The object.</param>
    /// <returns>Such as "warm grey (#8A8580) three-seat sofa, decorated with cushions".</returns>
    public static string Describe(PlanItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return $"{Named(item)}{Decorations(item)}";
    }

    /// <summary>An object's name with its color: before it ("warm grey (#8A8580) sofa"), or as its frame's for a picture.</summary>
    /// <param name="item">The object.</param>
    private static string Named(PlanItem item)
    {
        return item.Shape is ItemShape.Picture ? B.Prompt.Framed(item.Label, Color(item.Color)) : $"{Color(item.Color)} {item.Label}";
    }

    /// <summary>A color as words and its code, such as "sage green (#A8B89A)".</summary>
    /// <param name="color">The color.</param>
    private static string Color(HexColor color)
    {
        return $"{ColorName(color)} ({color.Value})";
    }

    /// <summary>The nearest named color, such as "sage green".</summary>
    /// <param name="color">The color.</param>
    private static string ColorName(HexColor color)
    {
        var (r, g, b) = Rgb(color.Value);
        var name = B.ColorNames
            .MinBy(n =>
            {
                var (nr, ng, nb) = Rgb(n.Hex);

                return (r - nr) * (r - nr) + (g - ng) * (g - ng) + (b - nb) * (b - nb);
            })
            .Name;

        return name;
    }

    /// <summary>The red, green and blue of a #RRGGBB color.</summary>
    /// <param name="hex">The color.</param>
    private static (int R, int G, int B) Rgb(string hex)
    {
        var value = int.Parse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        return ((value >> 16) & 0xFF, (value >> 8) & 0xFF, value & 0xFF);
    }

    /// <summary>Centimetres as a whole number.</summary>
    /// <param name="centimetres">The length.</param>
    private static string Cm(double centimetres)
    {
        return Math.Round(centimetres).ToString("0", CultureInfo.InvariantCulture);
    }

    /// <summary>An enum name as lower-case words: "LivingRoom" becomes "living room".</summary>
    /// <param name="name">The name.</param>
    /// <returns>The words.</returns>
    public static string Words(string name)
    {
        return CapitalLetter().Replace(name, " $1").Trim().ToLowerInvariant();
    }

    [GeneratedRegex("([A-Z])")]
    private static partial Regex CapitalLetter();

    /// <summary>How much of a thing the photo shows.</summary>
    /// <param name="Edges">The picture's edges that cut it (none when it is all in view), or null when it is out of view.</param>
    /// <param name="Hidden">Whether a wall of the room hides part of it from the camera.</param>
    private sealed record Seen(IReadOnlyList<string>? Edges, bool Hidden);

    /// <summary>
    /// How the plan is seen from the camera, which stands straight across the room from the middle of the wall in view
    /// (the far wall), a little in front of the wall facing it (the near wall), and looks at it; or in one of the near
    /// wall's corners, a little in from both walls.
    /// </summary>
    private sealed class Frame
    {
        private readonly double _leftmost;

        /// <summary>The frame for a room seen looking at one of its walls.</summary>
        /// <param name="outline">The room's walls.</param>
        /// <param name="view">The number of the wall in view.</param>
        /// <param name="corner">Where the camera stands: across from the wall's middle, or in a near corner.</param>
        public Frame(RoomOutline outline, int view, ViewCorner corner)
        {
            (Outline, View, Far, Corner) = (outline, view, outline.Walls[view], corner);
            var alongs = outline.Points.Select(p => Along(p.X, p.Y)).ToList();

            _leftmost = alongs.Min();
            Across = alongs.Max() - _leftmost;
            var facing = outline.Facing(view);

            Deep = facing?.Distance ?? outline.Points.Max(p => (p.X - Far.Start.X) * Far.NormalX + (p.Y - Far.Start.Y) * Far.NormalY);
            Near = facing?.Wall ?? -1;
            var fromLeft = Far.Length / 2 - _leftmost;

            if (corner != ViewCorner.None)
            {
                // The near wall's ends (the room's sides when there is none), a little in from the side wall.
                var near = facing is { } found ? Outline.Walls[found.Wall] : null;
                var ends = near is null ? new[] { 0, Across } : new[] { Along(near.Start.X, near.Start.Y) - _leftmost, Along(near.End.X, near.End.Y) - _leftmost };

                fromLeft = corner == ViewCorner.Left ? ends.Min() + C.Inset : ends.Max() - C.Inset;
            }

            var (along, stand) = (fromLeft + _leftmost, Deep - C.Inset);

            Camera = new OutlinePoint(Far.Start.X + Far.Dx * along + Far.NormalX * stand, Far.Start.Y + Far.Dy * along + Far.NormalY * stand);
            CameraFromLeft = fromLeft;
        }

        /// <summary>The room's walls.</summary>
        public RoomOutline Outline { get; }

        /// <summary>The number of the wall the photo looks at.</summary>
        public int View { get; }

        /// <summary>The wall the photo looks at.</summary>
        public OutlineWall Far { get; }

        /// <summary>Where the camera stands: across from the far wall's middle, or in a near corner.</summary>
        public ViewCorner Corner { get; }

        /// <summary>The room's size from the photo's left wall to its right wall.</summary>
        public double Across { get; }

        /// <summary>The room's size from the camera's wall to the far wall.</summary>
        public double Deep { get; }

        /// <summary>The number of the wall behind the camera, or -1 when there is none.</summary>
        public int Near { get; }

        /// <summary>Where the camera stands on the plan.</summary>
        public OutlinePoint Camera { get; }

        /// <summary>Where the camera stands from the photo's left wall.</summary>
        public double CameraFromLeft { get; }

        /// <summary>Whether the photo looks across the plan's width (at a wall running down the plan), so its across and deep swap.</summary>
        public bool Sideways => Far.Dx == 0;

        /// <summary>A point of the plan in the photo: its distance from the left wall and from the far wall.</summary>
        /// <param name="x">Across the plan, in centimetres.</param>
        /// <param name="y">Down the plan, in centimetres.</param>
        public (double FromLeft, double FromFar) Point(double x, double y)
        {
            return (Along(x, y) - _leftmost, (x - Far.Start.X) * Far.NormalX + (y - Far.Start.Y) * Far.NormalY);
        }

        /// <summary>Whether the camera sees a wall's face: it isn't the wall behind the camera and faces towards the camera.</summary>
        /// <param name="wall">The wall's number.</param>
        public bool Faces(int wall)
        {
            if (wall == Near)
            {
                return false;
            }

            var own = Outline.Walls[wall];
            var middle = own.Point(own.Length / 2);

            return (Camera.X - middle.X) * own.NormalX + (Camera.Y - middle.Y) * own.NormalY > 0;
        }

        /// <summary>Whether a wall runs the same way as the far wall (it faces the camera).</summary>
        /// <param name="wall">The wall's number.</param>
        public bool Parallel(int wall)
        {
            return (Outline.Walls[wall].Dx == 0) == Sideways;
        }

        /// <summary>Whether a side wall is on the photo's left: it faces right.</summary>
        /// <param name="wall">The wall's number.</param>
        public bool OnLeft(int wall)
        {
            var own = Outline.Walls[wall];

            return own.NormalX * Far.Dx + own.NormalY * Far.Dy > 0;
        }

        /// <summary>Where a point along a wall is, in words: on the far wall, a side wall, or (rooms that aren't rectangles) another wall.</summary>
        /// <param name="wall">The wall's number.</param>
        /// <param name="along">From the wall's left end as seen from inside facing it, in centimetres.</param>
        public string Place(int wall, double along)
        {
            var point = Outline.Walls[wall].Point(along);
            var (fromLeft, fromFar) = Point(point.X, point.Y);

            if (wall == View)
            {
                return B.Prompt.OnFarWall(Cm(fromLeft));
            }

            if (Outline.IsRectangle)
            {
                return B.Prompt.OnSideWall(OnLeft(wall), Cm(fromFar));
            }

            return Parallel(wall) ? B.Prompt.OnStepWall(Cm(fromFar), Cm(fromLeft)) : B.Prompt.OnSideWallAt(OnLeft(wall), Cm(fromLeft), Cm(fromFar));
        }

        /// <summary>Which way an object turned on the plan faces in the photo.</summary>
        /// <param name="rotation">Its turn, clockwise from above; at 0 it faces the plan's front.</param>
        public string Facing(int rotation)
        {
            // The direction its front faces on the plan (x across, y down towards the front).
            var (dx, dy) = (((rotation % 360) + 360) % 360) switch
            {
                90 => (-1, 0),
                180 => (0, -1),
                270 => (1, 0),
                _ => (0, 1),
            };
            // The same direction in the photo: to the right (along the far wall), and towards the camera (its normal).
            var (right, toward) = (dx * Far.Dx + dy * Far.Dy, dx * Far.NormalX + dy * Far.NormalY);

            if (Corner != ViewCorner.None)
            {
                return toward > 0 ? B.Prompt.FacingNearWall : toward < 0 ? B.Prompt.FacingAway : right > 0 ? B.Prompt.FacingRightWall : B.Prompt.FacingLeftWall;
            }

            return toward > 0 ? B.Prompt.FacingCamera : toward < 0 ? B.Prompt.FacingAway : right > 0 ? B.Prompt.FacingRight : B.Prompt.FacingLeft;
        }

        /// <summary>How far along the far wall a point of the plan is, from the far wall's left end.</summary>
        /// <param name="x">Across the plan.</param>
        /// <param name="y">Down the plan.</param>
        private double Along(double x, double y)
        {
            return (x - Far.Start.X) * Far.Dx + (y - Far.Start.Y) * Far.Dy;
        }
    }
}
