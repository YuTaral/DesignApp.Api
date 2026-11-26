using DesignApp.Domain.Extensions;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;
using SL = DesignApp.Application.Constants.Builder.Layout;

namespace DesignApp.Application.Helpers;

/// <summary>
/// The cleanup pass for a plan read from a photo, whose objects are placed roughly and may overlap. Windows and doors
/// are lowered under the ceiling and slid along their wall to where they fit, and the floor where the camera stands is
/// kept clear; then cabinets, sofas and beds turned with their front to a wall and their back to
/// the room are turned round, those near a wall are pushed against it, and every object
/// on the floor (the biggest first) is moved to the nearest free spot when it overlaps another, stands in front of a
/// door or a window it is taller than, or sticks out of the room; hung things slide along their wall to a clear spot;
/// rugs are kept inside the room. What fits nowhere near where it was is left out.
/// </summary>
public static class PhotoLayout
{
    /// <summary>The plan's objects, tidied.</summary>
    /// <param name="width">The room's width, in metres.</param>
    /// <param name="length">The room's length, in metres.</param>
    /// <param name="height">The ceiling height, in metres.</param>
    /// <param name="items">The objects as read from the photo.</param>
    /// <param name="camera">Where the photo was taken from, in centimetres, whose floor is kept clear; none when missing.</param>
    /// <returns>The objects kept, and the names of those left out.</returns>
    public static (IReadOnlyList<PlanItem> Items, IReadOnlyList<string> LeftOut) Tidy(double width, double length, double height, IEnumerable<PlanItem> items,
        OutlinePoint? camera = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        var room = new LayoutRoom(width * 100, length * 100, height * 100, []);
        var all = items.ToList();
        var leftOut = new List<string>();
        var standing = all.Where(i => !i.Shape.IsOnWall() && !i.Shape.IsZone() && i.Shape != ItemShape.Rug).OrderByDescending(i => i.Width * i.Depth);
        var steps = (int)(SL.NudgeReach / SL.Step);

        foreach (var item in all.Where(i => i.Shape.IsOpening()))
        {
            Keep(Slid(item, height * 100, steps, room.AddOpening), item, leftOut);
        }

        if (camera is not null)
        {
            room.Reserve(camera.X, camera.Y, SL.CameraClearance);
        }

        foreach (var item in standing)
        {
            var start = Inside(room, Snapped(room, TurnedRound(room, item)));
            var placed = Rings(steps).Select(o => room.Add(Moved(start, start.X + o.X * SL.Step, start.Y + o.Y * SL.Step))).FirstOrDefault(p => p is not null);

            Keep(placed, item, leftOut);
        }

        foreach (var item in all.Where(i => i.Shape.IsMounted()))
        {
            Keep(Slid(item, height * 100 - SL.CeilingGap, steps, room.HangItem), item, leftOut);
        }

        foreach (var item in all.Where(i => i.Shape == ItemShape.Rug))
        {
            Keep(room.Add(Inside(room, item)), item, leftOut);
        }

        return (room.Items, leftOut);
    }

    /// <summary>Notes an object left out when it found no place.</summary>
    /// <param name="placed">Where it was placed, or <c>null</c>.</param>
    /// <param name="item">The object.</param>
    /// <param name="leftOut">The names of those left out.</param>
    private static void Keep(PlanItem? placed, PlanItem item, List<string> leftOut)
    {
        if (placed is null)
        {
            leftOut.Add(item.Label);
        }
    }

    /// <summary>
    /// A thing on a wall lowered, if need be, so its top is under a height, then placed where it says or slid along its
    /// wall, a step at a time either way, to the nearest place it fits.
    /// </summary>
    /// <param name="item">The window, door or hung thing.</param>
    /// <param name="top">The highest its top may be, in centimetres.</param>
    /// <param name="steps">How many steps it may slide either way.</param>
    /// <param name="place">Places it, or gives <c>null</c> where it doesn't fit.</param>
    /// <returns>Where it was placed, or <c>null</c>.</returns>
    private static PlanItem? Slid(PlanItem item, double top, int steps, Func<PlanItem, PlanItem?> place)
    {
        var lowered = Moved(item, item.X, item.Y, Math.Max(0, Math.Min(item.Elevation, top - item.Height)));

        return Enumerable.Range(0, 2 * steps + 1)
            .Select(n => (n + 1) / 2 * (n % 2 == 0 ? -1 : 1))
            .Select(n => place(Moved(lowered, lowered.X + n * SL.Step, 0)))
            .FirstOrDefault(p => p is not null);
    }

    /// <summary>
    /// The offsets to try, in steps, nearest first: where it is, then each ring around it (the four sides, then the
    /// corners), out to a number of steps.
    /// </summary>
    /// <param name="steps">How many rings.</param>
    private static IEnumerable<(int X, int Y)> Rings(int steps)
    {
        yield return (0, 0);

        for (var r = 1; r <= steps; r++)
        {
            foreach (var offset in new[] { (r, 0), (-r, 0), (0, r), (0, -r), (r, r), (-r, r), (r, -r), (-r, -r) })
            {
                yield return offset;
            }
        }
    }

    /// <summary>
    /// A cabinet, sofa or bed turned round when its front is against a wall and its back to the room (a bed facing its
    /// headboard wall: the AI sometimes gives the opposite turn), so its back goes to the wall.
    /// </summary>
    /// <param name="room">The room.</param>
    /// <param name="item">The object.</param>
    private static PlanItem TurnedRound(LayoutRoom room, PlanItem item)
    {
        if (item.Shape is not (ItemShape.Box or ItemShape.Sofa or ItemShape.Bed))
        {
            return item;
        }

        var (back, front) = Gaps(room, item);

        if (front >= SL.SnapReach || back < SL.SnapReach)
        {
            return item;
        }

        return new PlanItem(item.Label, item.Shape, item.X, item.Y, item.Width, item.Depth, item.Height, (item.Rotation + 180) % 360, item.Color, item.Wall,
            item.Elevation, item.Form, item.Parts, item.Decorations, item.Material, item.Covering, item.Worktop);
    }

    /// <summary>
    /// A cabinet, sofa or bed pushed against the wall behind it when it stands within <see cref="SL.SnapReach"/> of it:
    /// its back faces the back wall at 0°, the right wall at 90°, the front wall at 180° and the left wall at 270°.
    /// </summary>
    /// <param name="room">The room.</param>
    /// <param name="item">The object.</param>
    private static PlanItem Snapped(LayoutRoom room, PlanItem item)
    {
        if (item.Shape is not (ItemShape.Box or ItemShape.Sofa or ItemShape.Bed))
        {
            return item;
        }

        var half = item.Depth / 2;

        if (Gaps(room, item).Back >= SL.SnapReach)
        {
            return item;
        }

        return (item.Rotation % 360) switch
        {
            90 => Moved(item, room.Width - half, item.Y),
            180 => Moved(item, item.X, room.Length - half),
            270 => Moved(item, half, item.Y),
            _ => Moved(item, item.X, half),
        };
    }

    /// <summary>
    /// How far an object's back and front are from the walls behind and in front of it: its back faces the back wall at
    /// 0°, the right wall at 90°, the front wall at 180° and the left wall at 270°.
    /// </summary>
    /// <param name="room">The room.</param>
    /// <param name="item">The object.</param>
    private static (double Back, double Front) Gaps(LayoutRoom room, PlanItem item)
    {
        var half = item.Depth / 2;

        return (item.Rotation % 360) switch
        {
            90 => (room.Width - (item.X + half), item.X - half),
            180 => (room.Length - (item.Y + half), item.Y - half),
            270 => (item.X - half, room.Width - (item.X + half)),
            _ => (item.Y - half, room.Length - (item.Y + half)),
        };
    }

    /// <summary>An object moved, if need be, so its footprint is inside the walls (its middle where the room is too small).</summary>
    /// <param name="room">The room.</param>
    /// <param name="item">The object on the floor.</param>
    private static PlanItem Inside(LayoutRoom room, PlanItem item)
    {
        var (across, down) = item.Rotation % 180 != 0 ? (item.Depth, item.Width) : (item.Width, item.Depth);

        return Moved(item, LayoutRoom.Within(item.X, across / 2, room.Width - across / 2), LayoutRoom.Within(item.Y, down / 2, room.Length - down / 2));
    }

    /// <summary>An object at another place (and, for a hung thing, another height), all else the same.</summary>
    /// <param name="item">The object.</param>
    /// <param name="x">Its new middle across the plan (along its wall for a hung thing).</param>
    /// <param name="y">Its new middle down the plan (0 for a hung thing).</param>
    /// <param name="elevation">Its new bottom above the floor; the same when missing.</param>
    private static PlanItem Moved(PlanItem item, double x, double y, double? elevation = null)
    {
        return new PlanItem(item.Label, item.Shape, Math.Round(x), Math.Round(y), item.Width, item.Depth, item.Height, item.Rotation, item.Color, item.Wall,
            elevation ?? item.Elevation, item.Form, item.Parts, item.Decorations, item.Material, item.Covering, item.Worktop);
    }
}
