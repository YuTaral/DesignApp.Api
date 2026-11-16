using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Objects;

/// <summary>
/// One wall of a room's outline, from one corner to the next going clockwise (y points down the plan). Along it runs
/// from its start, which is the wall's left end seen from inside the room facing it; its inside normal points into the
/// room. A rectangle's walls are its back, right, front and left walls, in that order.
/// </summary>
/// <param name="Start">Its first corner.</param>
/// <param name="End">Its last corner.</param>
public sealed record OutlineWall(OutlinePoint Start, OutlinePoint End)
{
    /// <summary>Its direction across the plan: 1, -1, or 0 for a wall running down the plan.</summary>
    public int Dx => Math.Sign(End.X - Start.X);

    /// <summary>Its direction down the plan: 1, -1, or 0 for a wall running across the plan.</summary>
    public int Dy => Math.Sign(End.Y - Start.Y);

    /// <summary>Its length, in centimetres.</summary>
    public double Length => Math.Abs(End.X - Start.X) + Math.Abs(End.Y - Start.Y);

    /// <summary>Its normal into the room, across the plan.</summary>
    public int NormalX => -Dy;

    /// <summary>Its normal into the room, down the plan.</summary>
    public int NormalY => Dx;

    /// <summary>The side of the plan it is on, from its direction: running right it is a back wall, down a right wall...</summary>
    public PlanWall Side => (Dx, Dy) switch
    {
        (1, _) => PlanWall.Back,
        (_, 1) => PlanWall.Right,
        (-1, _) => PlanWall.Front,
        _ => PlanWall.Left,
    };

    /// <summary>A point along it, in plan centimetres.</summary>
    /// <param name="along">From its start (its left end seen from inside).</param>
    public OutlinePoint Point(double along)
    {
        return new OutlinePoint(Start.X + Dx * along, Start.Y + Dy * along);
    }
}
