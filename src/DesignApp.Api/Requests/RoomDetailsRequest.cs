using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Api.Requests;

/// <summary>A built room's architecture, as sent and as returned; mouldings, wainscoting and casings are in the trim color.</summary>
/// <param name="Ceiling">How the ceiling is shaped.</param>
/// <param name="BeamColor">The exposed beams' color as #RRGGBB.</param>
/// <param name="Mouldings">The mouldings along the walls, each once; missing for none.</param>
/// <param name="Wainscot">The panelling on the lower part of the walls.</param>
/// <param name="Casing">The trim around the doors, windows and archways.</param>
public sealed record RoomDetailsRequest(CeilingStyle Ceiling, string? BeamColor, IReadOnlyList<Moulding>? Mouldings, Wainscot Wainscot, Casing Casing)
{
    /// <summary>The details as the domain keeps them.</summary>
    /// <exception cref="Domain.Exceptions.DomainException">A color or choice is invalid.</exception>
    public RoomDetails ToDomain()
    {
        return new RoomDetails(Ceiling, HexColor.Parse(BeamColor ?? ""), Mouldings ?? [], Wainscot, Casing);
    }

    /// <summary>The details as the API sends them.</summary>
    /// <param name="details">The details.</param>
    public static RoomDetailsRequest From(RoomDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        return new(details.Ceiling, details.BeamColor.Value, details.Mouldings, details.Wainscot, details.Casing);
    }
}
