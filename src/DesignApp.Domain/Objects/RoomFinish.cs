using DesignApp.Domain.Exceptions;

using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Objects;

/// <summary>
/// How a built room's walls and ceiling are finished and lit: the walls' finish, one accent wall in its own color and
/// finish, the ceiling's and the skirting boards' colors, the ceiling light, and the time of day its photo
/// is taken at.
/// </summary>
public sealed record RoomFinish
{
    /// <summary>Painted walls, no accent wall, a soft white ceiling, warm white trim, a pendant light, at midday.</summary>
    public static readonly RoomFinish Default = new(WallFinish.Paint, null, HexColor.Parse(Constants.Plans.AccentColor), WallFinish.Paint,
        HexColor.Parse(Constants.Plans.CeilingColor), HexColor.Parse(Constants.Plans.TrimColor), CeilingLight.Pendant, TimeOfDay.Midday);

    /// <summary>What the walls are finished in.</summary>
    public WallFinish Walls { get; }

    /// <summary>The number of the wall in its own color and finish (see <see cref="RoomOutline"/>); <c>null</c> for none.</summary>
    public int? AccentWall { get; }

    /// <summary>The accent wall's color.</summary>
    public HexColor AccentColor { get; }

    /// <summary>What the accent wall is finished in.</summary>
    public WallFinish AccentFinish { get; }

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
    /// <param name="accentWall">The accent wall's number; <c>null</c> for none.</param>
    /// <param name="accentColor">The accent wall's color.</param>
    /// <param name="accentFinish">What the accent wall is finished in.</param>
    /// <param name="ceilingColor">The ceiling's color.</param>
    /// <param name="trimColor">The skirting boards' color.</param>
    /// <param name="ceilingLight">The ceiling light.</param>
    /// <param name="timeOfDay">When the photo is taken.</param>
    /// <exception cref="DomainException">A choice isn't one of the lists, or the accent wall's number is negative.</exception>
    public RoomFinish(WallFinish walls, int? accentWall, HexColor accentColor, WallFinish accentFinish, HexColor ceilingColor, HexColor trimColor,
        CeilingLight ceilingLight, TimeOfDay timeOfDay)
    {
        if (!Enum.IsDefined(walls) || !Enum.IsDefined(accentFinish) || !Enum.IsDefined(ceilingLight) || !Enum.IsDefined(timeOfDay))
        {
            throw new DomainException(Constants.Messages.PlanFinish);
        }

        if (accentWall < 0)
        {
            throw new DomainException(Constants.Messages.PlanAccentWall);
        }

        (Walls, AccentWall, AccentColor, AccentFinish) = (walls, accentWall, accentColor, accentFinish);
        (CeilingColor, TrimColor, CeilingLight, TimeOfDay) = (ceilingColor, trimColor, ceilingLight, timeOfDay);
    }

    /// <summary>This finish with another accent wall (or none), the rest the same.</summary>
    /// <param name="wall">The accent wall's number; <c>null</c> for none.</param>
    public RoomFinish WithAccentWall(int? wall)
    {
        return new RoomFinish(Walls, wall, AccentColor, AccentFinish, CeilingColor, TrimColor, CeilingLight, TimeOfDay);
    }
}
