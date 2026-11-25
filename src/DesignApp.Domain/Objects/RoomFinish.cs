using DesignApp.Domain.Exceptions;

using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Objects;

/// <summary>
/// How a built room's walls and ceiling are finished and lit: the walls' finish (each wall may have its own finish and
/// color instead), the ceiling's and the skirting boards' colors, the ceiling light, and the time of day its photo is
/// taken at.
/// </summary>
public sealed record RoomFinish
{
    /// <summary>Painted walls, none in its own look, a soft white ceiling, warm white trim, a pendant light, at midday.</summary>
    public static readonly RoomFinish Default = new(WallFinish.Paint, [], HexColor.Parse(Constants.Plans.CeilingColor), HexColor.Parse(Constants.Plans.TrimColor),
        CeilingLight.Pendant, TimeOfDay.Midday);

    /// <summary>What the walls are finished in (in the room's wall color), but those in <see cref="OwnWalls"/>.</summary>
    public WallFinish Walls { get; }

    /// <summary>The walls in their own finish and color, by wall number; each wall at most once.</summary>
    public IReadOnlyList<WallLook> OwnWalls { get; }

    /// <summary>The ceiling's color.</summary>
    public HexColor CeilingColor { get; }

    /// <summary>The skirting boards' color (windows and doors keep their own colors).</summary>
    public HexColor TrimColor { get; }

    /// <summary>The light on the ceiling.</summary>
    public CeilingLight CeilingLight { get; }

    /// <summary>When the photo is taken.</summary>
    public TimeOfDay TimeOfDay { get; }

    /// <summary>Creates a finish.</summary>
    /// <param name="walls">What the walls are finished in.</param>
    /// <param name="ownWalls">The walls in their own finish and color; each wall at most once.</param>
    /// <param name="ceilingColor">The ceiling's color.</param>
    /// <param name="trimColor">The skirting boards' color.</param>
    /// <param name="ceilingLight">The ceiling light.</param>
    /// <param name="timeOfDay">When the photo is taken.</param>
    /// <exception cref="DomainException">A choice isn't one of the lists, or a wall has its own look twice.</exception>
    public RoomFinish(WallFinish walls, IReadOnlyList<WallLook> ownWalls, HexColor ceilingColor, HexColor trimColor, CeilingLight ceilingLight, TimeOfDay timeOfDay)
    {
        ArgumentNullException.ThrowIfNull(ownWalls);

        if (!Enum.IsDefined(walls) || !Enum.IsDefined(ceilingLight) || !Enum.IsDefined(timeOfDay))
        {
            throw new DomainException(Constants.Messages.PlanFinish);
        }

        if (ownWalls.DistinctBy(w => w.Wall).Count() != ownWalls.Count)
        {
            throw new DomainException(Constants.Messages.PlanOwnWalls);
        }

        (Walls, OwnWalls) = (walls, [.. ownWalls.OrderBy(w => w.Wall)]);
        (CeilingColor, TrimColor, CeilingLight, TimeOfDay) = (ceilingColor, trimColor, ceilingLight, timeOfDay);
    }

    /// <summary>This finish without the walls' own looks of walls a room no longer has, the rest the same.</summary>
    /// <param name="wallCount">How many walls the room has.</param>
    public RoomFinish Within(int wallCount)
    {
        return new RoomFinish(Walls, [.. OwnWalls.Where(w => w.Wall < wallCount)], CeilingColor, TrimColor, CeilingLight, TimeOfDay);
    }

    /// <summary>A wall's finish and color: its own, or the room's.</summary>
    /// <param name="wall">The wall's number.</param>
    /// <param name="wallColor">The room's wall color.</param>
    public (WallFinish Finish, HexColor Color) LookOf(int wall, HexColor wallColor)
    {
        return OwnWalls.FirstOrDefault(w => w.Wall == wall) is { } own ? (own.Finish, own.Color) : (Walls, wallColor);
    }

    /// <inheritdoc/>
    public bool Equals(RoomFinish? other)
    {
        return other is not null && (Walls, CeilingColor, TrimColor, CeilingLight, TimeOfDay) == (other.Walls, other.CeilingColor, other.TrimColor, other.CeilingLight, other.TimeOfDay)
            && OwnWalls.SequenceEqual(other.OwnWalls);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();

        hash.Add((Walls, CeilingColor, TrimColor, CeilingLight, TimeOfDay));

        foreach (var wall in OwnWalls)
        {
            hash.Add(wall);
        }

        return hash.ToHashCode();
    }
}
