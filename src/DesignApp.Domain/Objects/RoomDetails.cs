using DesignApp.Domain.Exceptions;

using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Objects;

/// <summary>
/// A built room's architecture, for the whole room: how its ceiling is shaped (and its beams' color), the mouldings run
/// along its walls, the panelling on their lower part and the casings around its doors, windows and archways. Mouldings,
/// wainscoting and casings are in the room's trim color.
/// </summary>
public sealed record RoomDetails
{
    /// <summary>A flat ceiling, oak beams if any, no mouldings or wainscoting, simple casings.</summary>
    public static readonly RoomDetails Default = new(CeilingStyle.Flat, HexColor.Parse(Constants.Plans.BeamColor), [], Wainscot.None, Casing.Simple);

    /// <summary>How the ceiling is shaped.</summary>
    public CeilingStyle Ceiling { get; }

    /// <summary>The exposed beams' color (kept while the ceiling has none).</summary>
    public HexColor BeamColor { get; }

    /// <summary>The mouldings along the walls, each once, in their order (crown, picture rail, dado rail).</summary>
    public IReadOnlyList<Moulding> Mouldings { get; }

    /// <summary>The panelling on the lower part of the walls.</summary>
    public Wainscot Wainscot { get; }

    /// <summary>The trim around the doors, windows and archways.</summary>
    public Casing Casing { get; }

    /// <summary>Creates a room's details.</summary>
    /// <param name="ceiling">How the ceiling is shaped.</param>
    /// <param name="beamColor">The exposed beams' color.</param>
    /// <param name="mouldings">The mouldings along the walls, each once.</param>
    /// <param name="wainscot">The panelling on the lower part of the walls.</param>
    /// <param name="casing">The trim around the doors, windows and archways.</param>
    /// <exception cref="DomainException">A choice isn't one of the lists, or a moulding is given twice.</exception>
    public RoomDetails(CeilingStyle ceiling, HexColor beamColor, IReadOnlyList<Moulding> mouldings, Wainscot wainscot, Casing casing)
    {
        ArgumentNullException.ThrowIfNull(mouldings);

        var known = Enum.IsDefined(ceiling) && Enum.IsDefined(wainscot) && Enum.IsDefined(casing) && mouldings.All(Enum.IsDefined);

        if (!known || mouldings.Distinct().Count() != mouldings.Count)
        {
            throw new DomainException(Constants.Messages.PlanDetails);
        }

        (Ceiling, BeamColor, Mouldings, Wainscot, Casing) = (ceiling, beamColor, [.. mouldings.Order()], wainscot, casing);
    }

    /// <summary>Whether the room has the moulding.</summary>
    /// <param name="moulding">The moulding.</param>
    public bool Has(Moulding moulding)
    {
        return Mouldings.Contains(moulding);
    }

    /// <inheritdoc/>
    public bool Equals(RoomDetails? other)
    {
        return other is not null && (Ceiling, BeamColor, Wainscot, Casing) == (other.Ceiling, other.BeamColor, other.Wainscot, other.Casing)
            && Mouldings.SequenceEqual(other.Mouldings);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();

        hash.Add((Ceiling, BeamColor, Wainscot, Casing));

        foreach (var moulding in Mouldings)
        {
            hash.Add(moulding);
        }

        return hash.ToHashCode();
    }
}
