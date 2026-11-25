using DesignApp.Domain.Exceptions;

using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Objects;

/// <summary>A wall of a built room in its own finish and color, instead of the room's.</summary>
public sealed record WallLook
{
    /// <summary>The wall's number (see <see cref="RoomOutline"/>).</summary>
    public int Wall { get; }

    /// <summary>What the wall is finished in.</summary>
    public WallFinish Finish { get; }

    /// <summary>The wall's color.</summary>
    public HexColor Color { get; }

    /// <summary>Creates a wall's own look.</summary>
    /// <param name="wall">The wall's number.</param>
    /// <param name="finish">What it is finished in.</param>
    /// <param name="color">Its color.</param>
    /// <exception cref="DomainException">The wall's number is negative, or the finish isn't one of the list.</exception>
    public WallLook(int wall, WallFinish finish, HexColor color)
    {
        if (wall < 0)
        {
            throw new DomainException(Constants.Messages.PlanOwnWalls);
        }

        if (!Enum.IsDefined(finish))
        {
            throw new DomainException(Constants.Messages.PlanFinish);
        }

        (Wall, Finish, Color) = (wall, finish, color);
    }
}
