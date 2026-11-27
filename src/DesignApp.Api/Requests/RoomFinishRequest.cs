using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Api.Requests;

/// <summary>How a built room's walls and ceiling are finished and lit, as sent and as returned.</summary>
/// <param name="Walls">What the walls are finished in (in the room's wall color), but those in <paramref name="OwnWalls"/>.</param>
/// <param name="OwnWalls">The walls in their own finish and color, each at most once; missing for none.</param>
/// <param name="CeilingColor">The ceiling's color as #RRGGBB.</param>
/// <param name="TrimColor">The trim's color as #RRGGBB: skirting, mouldings, wainscoting and casings (windows and doors keep their own colors).</param>
/// <param name="TimeOfDay">When the photo is taken, which sets its light and mood.</param>
/// <param name="Tone">The color of the lamps' and ceiling lights' light; warm when missing.</param>
/// <param name="North">The side of the plan that faces north (where the sun comes in from); the back when missing.</param>
/// <param name="Details">The room's architecture; a flat ceiling, no mouldings or wainscoting and simple casings when missing.</param>
/// <param name="Fittings">How the room is fitted (cabinet fronts, handles, hardware, sink, tap, basin, tile pattern); the defaults when missing.</param>
/// <param name="FloorMaterial">What the floor is made of within its type (such as "walnut"); any when missing or empty.</param>
/// <param name="WallSheen">How shiny the painted walls are; matte when missing.</param>
public sealed record RoomFinishRequest(WallFinish Walls, IReadOnlyList<WallLookRequest>? OwnWalls, string? CeilingColor, string? TrimColor,
    TimeOfDay TimeOfDay, LightTone Tone = LightTone.Warm, PlanWall North = PlanWall.Back, RoomDetailsRequest? Details = null, RoomFittings? Fittings = null,
    string? FloorMaterial = null, PaintSheen WallSheen = PaintSheen.Matte)
{
    /// <summary>The finish as the domain keeps it.</summary>
    /// <exception cref="Domain.Exceptions.DomainException">A color or choice is invalid.</exception>
    public RoomFinish ToDomain()
    {
        var own = (OwnWalls ?? []).Select(w => w.ToDomain()).ToList();

        return new RoomFinish(Walls, own, HexColor.Parse(CeilingColor ?? ""), HexColor.Parse(TrimColor ?? ""), TimeOfDay, Tone, North, Details?.ToDomain(), Fittings, FloorMaterial, WallSheen);
    }

    /// <summary>The finish as the API sends it.</summary>
    /// <param name="f">The finish.</param>
    public static RoomFinishRequest From(RoomFinish f)
    {
        ArgumentNullException.ThrowIfNull(f);

        return new(f.Walls, [.. f.OwnWalls.Select(WallLookRequest.From)], f.CeilingColor.Value, f.TrimColor.Value, f.TimeOfDay, f.Tone, f.North,
            RoomDetailsRequest.From(f.Details), f.Fittings, f.FloorMaterial, f.WallSheen);
    }
}
