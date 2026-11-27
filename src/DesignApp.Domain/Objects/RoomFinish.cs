using DesignApp.Domain.Exceptions;

using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Objects;

/// <summary>
/// How a built room's walls and ceiling are finished and lit: the walls' finish (each wall may have its own finish and
/// color instead), the ceiling's and the trim's (skirting, mouldings, casings) colors, the time of day its photo is
/// taken at, the color of its lamps' and ceiling lights' light, which side of the plan faces north (where the sun comes
/// from), and its architecture (<see cref="RoomDetails"/>). The ceiling lights themselves are objects on the plan.
/// </summary>
public sealed record RoomFinish
{
    /// <summary>Painted walls, none in its own look, a soft white ceiling, warm white trim, at midday, warm light, north at the back.</summary>
    public static readonly RoomFinish Default = new(WallFinish.Paint, [], HexColor.Parse(Constants.Plans.CeilingColor), HexColor.Parse(Constants.Plans.TrimColor),
        TimeOfDay.Midday);

    /// <summary>What the walls are finished in (in the room's wall color), but those in <see cref="OwnWalls"/>.</summary>
    public WallFinish Walls { get; }

    /// <summary>The walls in their own finish and color, by wall number; each wall at most once.</summary>
    public IReadOnlyList<WallLook> OwnWalls { get; }

    /// <summary>The ceiling's color.</summary>
    public HexColor CeilingColor { get; }

    /// <summary>The trim's color: the skirting, the mouldings, the wainscoting and the casings (windows and doors keep their own colors).</summary>
    public HexColor TrimColor { get; }

    /// <summary>When the photo is taken.</summary>
    public TimeOfDay TimeOfDay { get; }

    /// <summary>The color of the lamps' and ceiling lights' light.</summary>
    public LightTone Tone { get; }

    /// <summary>The side of the plan that faces north, which decides where the sun comes in from at each time of day.</summary>
    public PlanWall North { get; }

    /// <summary>The room's architecture: its ceiling's shape, mouldings, wainscoting and casings.</summary>
    public RoomDetails Details { get; }

    /// <summary>How the room is fitted: its cabinets' fronts and handles, the hardware, the sink, tap and basin, and the tile pattern.</summary>
    public RoomFittings Fittings { get; }

    /// <summary>Creates a finish.</summary>
    /// <param name="walls">What the walls are finished in.</param>
    /// <param name="ownWalls">The walls in their own finish and color; each wall at most once.</param>
    /// <param name="ceilingColor">The ceiling's color.</param>
    /// <param name="trimColor">The trim's color.</param>
    /// <param name="timeOfDay">When the photo is taken.</param>
    /// <param name="tone">The color of the lamps' and ceiling lights' light.</param>
    /// <param name="north">The side of the plan that faces north.</param>
    /// <param name="details">The room's architecture; <see cref="RoomDetails.Default"/> when missing.</param>
    /// <param name="fittings">How the room is fitted; <see cref="RoomFittings.Default"/> when missing.</param>
    /// <exception cref="DomainException">A choice isn't one of the lists, or a wall has its own look twice.</exception>
    public RoomFinish(WallFinish walls, IReadOnlyList<WallLook> ownWalls, HexColor ceilingColor, HexColor trimColor, TimeOfDay timeOfDay,
        LightTone tone = LightTone.Warm, PlanWall north = PlanWall.Back, RoomDetails? details = null, RoomFittings? fittings = null)
    {
        ArgumentNullException.ThrowIfNull(ownWalls);

        if (!Enum.IsDefined(walls) || !Enum.IsDefined(timeOfDay) || !Enum.IsDefined(tone) || !Enum.IsDefined(north))
        {
            throw new DomainException(Constants.Messages.PlanFinish);
        }

        if (ownWalls.DistinctBy(w => w.Wall).Count() != ownWalls.Count)
        {
            throw new DomainException(Constants.Messages.PlanOwnWalls);
        }

        (Walls, OwnWalls) = (walls, [.. ownWalls.OrderBy(w => w.Wall)]);
        (CeilingColor, TrimColor, TimeOfDay, Tone, North, Details) = (ceilingColor, trimColor, timeOfDay, tone, north, details ?? RoomDetails.Default);
        Fittings = (fittings ?? RoomFittings.Default).Checked();
    }

    /// <summary>This finish without the walls' own looks of walls a room no longer has, the rest the same.</summary>
    /// <param name="wallCount">How many walls the room has.</param>
    public RoomFinish Within(int wallCount)
    {
        return new RoomFinish(Walls, [.. OwnWalls.Where(w => w.Wall < wallCount)], CeilingColor, TrimColor, TimeOfDay, Tone, North, Details, Fittings);
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
        return other is not null && (Walls, CeilingColor, TrimColor, TimeOfDay, Tone, North) == (other.Walls, other.CeilingColor, other.TrimColor, other.TimeOfDay, other.Tone, other.North)
            && Details.Equals(other.Details) && Fittings.Equals(other.Fittings) && OwnWalls.SequenceEqual(other.OwnWalls);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();

        hash.Add((Walls, CeilingColor, TrimColor, TimeOfDay, Tone, North));
        hash.Add(Details);
        hash.Add(Fittings);

        foreach (var wall in OwnWalls)
        {
            hash.Add(wall);
        }

        return hash.ToHashCode();
    }
}
