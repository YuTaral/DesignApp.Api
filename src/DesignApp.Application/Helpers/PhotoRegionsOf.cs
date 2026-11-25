using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Extensions;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.Helpers;

/// <summary>Which regions of a photo made from a built room get their colors corrected (see <see cref="PhotoRegions"/>).</summary>
public static class PhotoRegionsOf
{
    /// <summary>
    /// The regions of a room's photos: every object but windows and doors (a picture's masks show only its frame, the
    /// color planned for it); the floor, walls and ceiling with the colors planned on them, areas in other materials
    /// and the accent wall included; and, at any time of day but midday, the photo's own light kept.
    /// </summary>
    /// <param name="plan">The room.</param>
    /// <returns>The regions.</returns>
    public static PhotoRegions Plan(RoomPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var objects = plan.Items
            .Select((item, index) => (item, id: index + 1))
            .Where(o => !o.item.Shape.IsZone() && !Constants.Builder.UncorrectedShapes.Contains(o.item.Shape))
            .Select(o => o.id)
            .ToHashSet();
        var floors = new List<HexColor> { plan.FloorColor };
        var walls = new List<HexColor> { plan.WallColor };

        floors.AddRange(plan.Items.Where(i => i.Shape is ItemShape.FloorZone).Select(i => i.Color));
        walls.AddRange(plan.Items.Where(i => i.Shape is ItemShape.WallZone).Select(i => i.Color));

        walls.AddRange(plan.Finish.OwnWalls.Select(w => w.Color));

        var surfaces = new Dictionary<int, IReadOnlyList<HexColor>>
        {
            [Constants.Builder.MaskIds.Floor] = floors,
            [Constants.Builder.MaskIds.Wall] = walls,
            [Constants.Builder.MaskIds.Ceiling] = [plan.Finish.CeilingColor],
        };

        return new PhotoRegions(objects, surfaces, plan.Finish.TimeOfDay is not TimeOfDay.Midday);
    }
}
