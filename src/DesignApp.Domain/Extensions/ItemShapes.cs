using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Extensions;

/// <summary>Rules for <see cref="ItemShape"/>: which shapes are in a wall, on a wall, or stand on the floor.</summary>
public static class ItemShapes
{
    /// <summary>Whether a shape is an opening in a wall: a window or a door.</summary>
    /// <param name="shape">The shape.</param>
    public static bool IsOpening(this ItemShape shape)
    {
        return shape is ItemShape.Window or ItemShape.Door;
    }

    /// <summary>Whether a shape hangs on a wall: a picture (paintings, mirrors, a TV) or a wall box (upper cabinets, shelves, a hood).</summary>
    /// <param name="shape">The shape.</param>
    public static bool IsMounted(this ItemShape shape)
    {
        return shape is ItemShape.Picture or ItemShape.WallBox;
    }

    /// <summary>Whether a shape is a light on the ceiling (placed on the plan; its height is how far down it reaches).</summary>
    /// <param name="shape">The shape.</param>
    public static bool IsCeilingLight(this ItemShape shape)
    {
        return shape is ItemShape.PendantLight or ItemShape.Spotlight or ItemShape.Chandelier or ItemShape.CeilingLamp or ItemShape.TrackLight;
    }

    /// <summary>Whether a shape is an area of the floor or a wall in its own material: a surface, not furniture.</summary>
    /// <param name="shape">The shape.</param>
    public static bool IsZone(this ItemShape shape)
    {
        return shape is ItemShape.FloorZone or ItemShape.WallZone;
    }

    /// <summary>Whether a shape belongs to a wall (an opening, a mounted object or a wall area) rather than the floor.</summary>
    /// <param name="shape">The shape.</param>
    public static bool IsOnWall(this ItemShape shape)
    {
        return shape.IsOpening() || shape.IsMounted() || shape is ItemShape.WallZone;
    }

    /// <summary>How high a new object of a shape usually starts above the floor, in centimetres.</summary>
    /// <param name="shape">The shape.</param>
    /// <returns>A window's sill, a mounted object's usual height, 0 for the rest.</returns>
    public static double UsualElevation(this ItemShape shape)
    {
        return shape switch
        {
            ItemShape.Window => Plans.WindowSill,
            _ when shape.IsMounted() => Plans.MountedElevation,
            _ => 0,
        };
    }
}
