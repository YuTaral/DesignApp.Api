using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Extensions;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.Helpers;

/// <summary>
/// Turns a photo plan as the AI read it into what a room plan takes: sizes kept to a plan's range (centimetres read as
/// metres), the camera's spot kept inside the room, each object made from its catalog object and checked, then tidied
/// by <see cref="PhotoLayout"/>, which keeps the camera's spot clear.
/// </summary>
public static class PhotoPlanReader
{
    /// <summary>The objects a photo plan may use: every room type's, each once.</summary>
    /// <param name="catalog">The catalog.</param>
    public static IReadOnlyList<CatalogObject> Offered(IObjectCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return [.. Enum.GetValues<RoomType>().SelectMany(catalog.ForRoom).DistinctBy(o => o.Key)];
    }

    /// <summary>The photo plan made ready for a room plan.</summary>
    /// <param name="answer">The plan as the AI read it.</param>
    /// <param name="offered">The objects it could use (see <see cref="Offered"/>).</param>
    public static TidiedPhotoPlan Read(PhotoPlan answer, IReadOnlyList<CatalogObject> offered)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(offered);
        var plain = RoomFinish.Default;
        var (width, length, height) = (Metres(answer.Width, Plans.MinWallMetres, Plans.MaxWallMetres), Metres(answer.Length, Plans.MinWallMetres, Plans.MaxWallMetres),
            Metres(answer.Height, Plans.MinHeightMetres, Plans.MaxHeightMetres));
        var byKey = offered.ToDictionary(o => o.Key);
        var leftOut = new List<string>();
        var items = answer.Objects.Select(o => ToItem(o, byKey, width * 100, length * 100, leftOut)).OfType<PlanItem>().ToList();
        var inset = Constants.Builder.Camera.Inset;
        var camera = new OutlinePoint(Math.Round(Inside(answer.Camera.X, inset, width * 100 - inset)), Math.Round(Inside(answer.Camera.Y, inset, length * 100 - inset)));
        var (tidied, unplaced) = PhotoLayout.Tidy(width, length, height, items, camera);
        var wallColor = HexColor.TryParse(answer.WallColor, out var walls) ? walls : HexColor.Parse(Plans.DefaultWallColor);
        var floorColor = HexColor.TryParse(answer.FloorColor, out var floor) ? floor : HexColor.Parse(Plans.DefaultFloorColor);
        var finish = new RoomFinish(answer.Walls, null, plain.AccentColor, plain.AccentFinish, plain.CeilingColor, plain.TrimColor, answer.CeilingLight, plain.TimeOfDay);

        return new TidiedPhotoPlan(width, length, height, wallColor, floorColor, finish, camera, [.. tidied.Take(Plans.MaxItems)], [.. leftOut, .. unplaced]);
    }

    /// <summary>A place on the plan kept between two ends (a value that isn't a number is taken as the far one).</summary>
    /// <param name="value">The place, in centimetres.</param>
    /// <param name="min">The nearest it may be.</param>
    /// <param name="max">The farthest it may be.</param>
    private static double Inside(double value, double min, double max)
    {
        return double.IsFinite(value) ? Math.Clamp(value, min, max) : max;
    }

    /// <summary>
    /// A size the AI judged, kept to what a plan allows. One above the largest allowed is taken as centimetres (the AI
    /// once answered a 4.6 m room as 460), since no room is that big.
    /// </summary>
    /// <param name="value">The size, in metres (or centimetres when above <paramref name="max"/>).</param>
    /// <param name="min">The smallest allowed, in metres.</param>
    /// <param name="max">The largest allowed, in metres.</param>
    private static double Metres(double value, double min, double max)
    {
        var metres = value > max ? value / 100 : value;

        return double.IsFinite(metres) ? Math.Round(Math.Clamp(metres, min, max), 1) : min;
    }

    /// <summary>
    /// The wall nearest a place on the plan, and how far along it the place is, from the wall's left end as seen from
    /// inside facing it (the back wall's is the left corner, the right wall's its back corner, the left wall's its front corner).
    /// </summary>
    /// <param name="x">Across the plan, in centimetres.</param>
    /// <param name="y">Down the plan, in centimetres.</param>
    /// <param name="width">The room's width, in centimetres.</param>
    /// <param name="length">The room's length, in centimetres.</param>
    private static (PlanWall Wall, double Along) NearestWall(double x, double y, double width, double length)
    {
        (PlanWall Wall, double Distance, double Along)[] walls =
            [(PlanWall.Back, y, x), (PlanWall.Right, width - x, y), (PlanWall.Front, length - y, width - x), (PlanWall.Left, x, length - y)];
        var nearest = walls.MinBy(w => w.Distance);

        return (nearest.Wall, nearest.Along);
    }

    /// <summary>
    /// One object of the answer as a plan object: its catalog object's variation gives its name, shape and outline, the
    /// answer its place, size, color and material. A thing on a wall answered as standing on the floor goes on the wall
    /// nearest its place. One the domain refuses (a size out of range, say) is left out.
    /// </summary>
    /// <param name="o">The object as read.</param>
    /// <param name="byKey">The catalog by key.</param>
    /// <param name="width">The room's width, in centimetres.</param>
    /// <param name="length">The room's length, in centimetres.</param>
    /// <param name="leftOut">The names of the objects left out.</param>
    private static PlanItem? ToItem(PhotoPlanItem o, Dictionary<string, CatalogObject> byKey, double width, double length, List<string> leftOut)
    {
        if (!byKey.TryGetValue(o.Key, out var found))
        {
            return null;
        }

        var variant = found.Variants?.FirstOrDefault(v => v.Key == o.Variant) ?? (found.Variants is { Count: > 0 } all ? all[0] : null);
        var (label, shape, form) = variant is null ? (found.Label, found.Shape, found.Form) : (variant.Label, variant.Shape, variant.Form);
        var onWall = shape.IsOnWall();
        var color = HexColor.TryParse(o.Color, out var parsed) ? parsed : found.Color;
        var material = (found.Materials ?? []).Contains(o.Material) ? o.Material : found.Material;
        var (wall, along) = o.Wall is { } given ? (given, o.Along) : NearestWall(o.X, o.Y, width, length);

        try
        {
            return onWall
                ? new PlanItem(label, shape, along, 0, o.Width, o.Depth, o.Height, 0, color, (int)wall, o.Elevation, form, material: material)
                : new PlanItem(label, shape, o.X, o.Y, o.Width, o.Depth, o.Height, o.Rotation, color, form: form, material: material);
        }
        catch (DomainException)
        {
            leftOut.Add(label);

            return null;
        }
    }
}
