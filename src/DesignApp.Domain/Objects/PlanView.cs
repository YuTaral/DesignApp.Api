using DesignApp.Domain.Exceptions;

using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Objects;

/// <summary>
/// How a built room is seen in its 3D view and photos: the wall looked at, where the camera stands to look at it, and
/// the spot a photo the room was read from was taken (for <see cref="ViewCorner.Spot"/>). A wall number alone is a view
/// of that wall from its middle.
/// </summary>
/// <param name="Wall">The number of the wall in view.</param>
/// <param name="Corner">Where the camera stands to look at it.</param>
/// <param name="Spot">Where a photo the room was read from was taken, in centimetres; <c>null</c> when there is none.</param>
public sealed record PlanView(int Wall, ViewCorner Corner = ViewCorner.None, OutlinePoint? Spot = null)
{
    /// <summary>A view of a wall from its middle.</summary>
    /// <param name="wall">The number of the wall in view.</param>
    public static implicit operator PlanView(int wall)
    {
        return FromInt32(wall);
    }

    /// <summary>A view of a rectangular room's wall from its middle.</summary>
    /// <param name="wall">The wall in view.</param>
    public static implicit operator PlanView(PlanWall wall)
    {
        return FromPlanWall(wall);
    }

    /// <summary>A view of a wall from its middle.</summary>
    /// <param name="wall">The number of the wall in view.</param>
    public static PlanView FromInt32(int wall)
    {
        return new PlanView(wall);
    }

    /// <summary>A view of a rectangular room's wall from its middle.</summary>
    /// <param name="wall">The wall in view.</param>
    /// <exception cref="DomainException">The wall is no wall of a rectangle.</exception>
    public static PlanView FromPlanWall(PlanWall wall)
    {
        return Enum.IsDefined(wall) ? new PlanView((int)wall) : throw new DomainException(Constants.Messages.PlanLook);
    }

    /// <summary>
    /// The view kept to a room's walls: a spot that isn't inside them is dropped, and the spot's camera without a spot
    /// looks from the middle.
    /// </summary>
    /// <param name="outline">The room's walls.</param>
    /// <exception cref="DomainException">The wall is no wall of the room, or the camera's place is unknown.</exception>
    public PlanView Within(RoomOutline outline)
    {
        ArgumentNullException.ThrowIfNull(outline);

        if (Wall < 0 || Wall >= outline.Walls.Count || !Enum.IsDefined(Corner))
        {
            throw new DomainException(Constants.Messages.PlanLook);
        }

        var inside = Spot is { } at && double.IsFinite(at.X) && double.IsFinite(at.Y) && outline.Contains(at.X, at.Y) ? at : null;

        return new PlanView(Wall, Corner == ViewCorner.Spot && inside is null ? ViewCorner.None : Corner, inside);
    }
}
