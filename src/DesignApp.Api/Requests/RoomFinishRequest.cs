using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Api.Requests;

/// <summary>How a built room's walls and ceiling are finished and lit, as sent and as returned.</summary>
/// <param name="Walls">What the walls are finished in (in the room's wall color), but those in <paramref name="OwnWalls"/>.</param>
/// <param name="OwnWalls">The walls in their own finish and color, each at most once; missing for none.</param>
/// <param name="CeilingColor">The ceiling's color as #RRGGBB.</param>
/// <param name="TrimColor">The skirting boards' color as #RRGGBB (windows and doors keep their own colors).</param>
/// <param name="CeilingLight">The light on the ceiling.</param>
/// <param name="TimeOfDay">When the photo is taken, which sets its light and mood.</param>
public sealed record RoomFinishRequest(WallFinish Walls, IReadOnlyList<WallLookRequest>? OwnWalls, string? CeilingColor, string? TrimColor,
    CeilingLight CeilingLight, TimeOfDay TimeOfDay)
{
    /// <summary>The finish as the domain keeps it.</summary>
    /// <exception cref="Domain.Exceptions.DomainException">A color or choice is invalid.</exception>
    public RoomFinish ToDomain()
    {
        var own = (OwnWalls ?? []).Select(w => w.ToDomain()).ToList();

        return new RoomFinish(Walls, own, HexColor.Parse(CeilingColor ?? ""), HexColor.Parse(TrimColor ?? ""), CeilingLight, TimeOfDay);
    }

    /// <summary>The finish as the API sends it.</summary>
    /// <param name="f">The finish.</param>
    public static RoomFinishRequest From(RoomFinish f)
    {
        ArgumentNullException.ThrowIfNull(f);

        return new(f.Walls, [.. f.OwnWalls.Select(WallLookRequest.From)], f.CeilingColor.Value, f.TrimColor.Value, f.CeilingLight, f.TimeOfDay);
    }
}
