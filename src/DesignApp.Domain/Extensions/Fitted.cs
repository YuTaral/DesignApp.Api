using System.Text.RegularExpressions;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Extensions;

/// <summary>
/// What of a built room's fittings it has, by its objects' names and its surfaces: cabinetry (kitchen units, an island,
/// a vanity), upper cabinets, a kitchen sink unit, a vanity, and tiles anywhere (walls, the floor, an area, a backsplash).
/// </summary>
public static partial class Fitted
{
    /// <summary>Whether an object is fitted cabinetry, by its name.</summary>
    /// <param name="item">The object.</param>
    public static bool IsCabinet(this PlanItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.Shape is ItemShape.Box or ItemShape.WallBox && Cabinet().IsMatch(item.Label);
    }

    /// <summary>Whether the room has fitted cabinetry.</summary>
    /// <param name="plan">The room.</param>
    public static bool HasCabinets(this RoomPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return plan.Items.Any(IsCabinet);
    }

    /// <summary>Whether the room has cabinets hung on a wall (upper cabinets, a mirror cabinet).</summary>
    /// <param name="plan">The room.</param>
    public static bool HasUpperCabinets(this RoomPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return plan.Items.Any(i => i.Shape is ItemShape.WallBox && i.IsCabinet());
    }

    /// <summary>Whether the room has a kitchen sink unit.</summary>
    /// <param name="plan">The room.</param>
    public static bool HasSink(this RoomPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return plan.Items.Any(i => i.Wall is null && Sink().IsMatch(i.Label));
    }

    /// <summary>Whether the room has a vanity, with its basin.</summary>
    /// <param name="plan">The room.</param>
    public static bool HasVanity(this RoomPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return plan.Items.Any(i => i.Wall is null && Vanity().IsMatch(i.Label));
    }

    /// <summary>Whether the room has tiles anywhere: its walls (or one of them), its floor, an area of either, or a backsplash.</summary>
    /// <param name="plan">The room.</param>
    public static bool UsesTiles(this RoomPlan plan)
    {
        var (walls, floor, backsplash) = plan.Tiled();

        return walls || floor || backsplash;
    }

    /// <summary>
    /// Where the room has tiles: on its walls (all of them, one of them, or an area of one), on its floor (or an area of
    /// it), and on a kitchen unit's backsplash.
    /// </summary>
    /// <param name="plan">The room.</param>
    public static (bool Walls, bool Floor, bool Backsplash) Tiled(this RoomPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var finish = plan.Finish;
        var walls = finish.Walls is WallFinish.Tiles || finish.OwnWalls.Any(w => w.Finish is WallFinish.Tiles)
            || plan.Items.Any(i => i.Shape is ItemShape.WallZone && i.Material is nameof(WallFinish.Tiles));
        var floor = plan.FloorType is FloorType.Tiles || plan.Items.Any(i => i.Shape is ItemShape.FloorZone && i.Material is nameof(FloorType.Tiles));
        var backsplash = plan.Items.Any(i => i.Worktop?.Backsplash is BacksplashKind.Tiles);

        return (walls, floor, backsplash);
    }

    [GeneratedRegex(Plans.CabinetWords, RegexOptions.IgnoreCase)]
    private static partial Regex Cabinet();

    [GeneratedRegex(Plans.SinkWords, RegexOptions.IgnoreCase)]
    private static partial Regex Sink();

    [GeneratedRegex(Plans.VanityWords, RegexOptions.IgnoreCase)]
    private static partial Regex Vanity();
}
