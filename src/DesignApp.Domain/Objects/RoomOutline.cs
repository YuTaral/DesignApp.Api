using DesignApp.Domain.Exceptions;

using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Objects;

/// <summary>
/// A room's outline on the floor plan: its corners in centimetres, clockwise (y down the plan) from the top-left one at
/// 0, 0. Its walls are the edges between them, numbered from that corner; they run across or down the plan in turn, so
/// walls only bend at right angles (an L, T or U room, or an alcove). A rectangle's walls are numbered back, right,
/// front and left, as <see cref="PlanWall"/> lists them. Two outlines are equal when their corners are.
/// </summary>
public sealed class RoomOutline : IEquatable<RoomOutline>
{
    /// <summary>The corners, clockwise from the top-left one.</summary>
    public IReadOnlyList<OutlinePoint> Points { get; }

    /// <summary>The walls, wall i running from corner i to the next.</summary>
    public IReadOnlyList<OutlineWall> Walls { get; }

    /// <summary>Checks and keeps an outline.</summary>
    /// <param name="points">The corners, clockwise from the top-left one at 0, 0, in whole centimetres.</param>
    /// <exception cref="DomainException">
    /// There are too few or too many corners, a wall is too short or slanted, two walls cross, the corners run the wrong
    /// way or don't start at the top-left, or the room is too small or too big.
    /// </exception>
    public RoomOutline(IEnumerable<OutlinePoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var list = points.ToList();

        if (!IsValid(list))
        {
            throw new DomainException(Constants.Messages.PlanOutline);
        }

        Points = list.AsReadOnly();
        Walls = list.Select((p, i) => new OutlineWall(p, list[(i + 1) % list.Count])).ToList().AsReadOnly();
    }

    /// <summary>A rectangular room of a size, its walls numbered back, right, front, left.</summary>
    /// <param name="widthMetres">Its width, across the plan.</param>
    /// <param name="lengthMetres">Its length, down the plan.</param>
    /// <exception cref="DomainException">The room is too small or too big.</exception>
    public static RoomOutline Rectangle(double widthMetres, double lengthMetres)
    {
        var (w, l) = (Math.Round(widthMetres * 100), Math.Round(lengthMetres * 100));

        return new RoomOutline([new(0, 0), new(w, 0), new(w, l), new(0, l)]);
    }

    /// <summary>The room's overall width (its outline's widest extent across the plan), in metres.</summary>
    public double WidthMetres => Points.Max(p => p.X) / 100;

    /// <summary>The room's overall length (its outline's longest extent down the plan), in metres.</summary>
    public double LengthMetres => Points.Max(p => p.Y) / 100;

    /// <summary>Whether it is a plain rectangle.</summary>
    public bool IsRectangle => Points.Count == 4;

    /// <summary>How long a wall is, in centimetres.</summary>
    /// <param name="wall">The wall's number.</param>
    public double WallLength(int wall)
    {
        return Walls[wall].Length;
    }

    /// <summary>Whether a point of the plan is inside the room (on a wall counts as inside).</summary>
    /// <param name="x">Across the plan, in centimetres.</param>
    /// <param name="y">Down the plan, in centimetres.</param>
    public bool Contains(double x, double y)
    {
        if (Walls.Any(w => OnSegment(w, x, y)))
        {
            return true;
        }

        // Count the vertical walls a ray to the right crosses.
        var crossings = Walls.Count(w => w.Dx == 0 && w.Start.X > x && y >= Math.Min(w.Start.Y, w.End.Y) && y < Math.Max(w.Start.Y, w.End.Y));

        return crossings % 2 == 1;
    }

    /// <summary>
    /// The wall straight across the room from the middle of a wall: the nearest wall facing back at it whose span covers
    /// that middle, and how far away it is. For a rectangle it is the opposite wall, the room's width or length away.
    /// </summary>
    /// <param name="wall">The wall's number.</param>
    /// <returns>The facing wall's number and its distance in centimetres, or null when there is none.</returns>
    public (int Wall, double Distance)? Facing(int wall)
    {
        var own = Walls[wall];
        var middle = own.Point(own.Length / 2);
        var facing = Walls
            .Select((other, index) => (Other: other, Index: index))
            .Where(w => w.Other.Dx == -own.Dx && w.Other.Dy == -own.Dy)
            .Select(w => (w.Index, w.Other, Distance: (w.Other.Start.X - middle.X) * own.NormalX + (w.Other.Start.Y - middle.Y) * own.NormalY))
            .Where(w => w.Distance > 0 && (own.Dx != 0
                ? Between(middle.X, w.Other.Start.X, w.Other.End.X)
                : Between(middle.Y, w.Other.Start.Y, w.Other.End.Y)))
            .OrderBy(w => w.Distance)
            .Select(w => ((int Wall, double Distance)?)(w.Index, w.Distance))
            .FirstOrDefault();

        return facing;
    }

    /// <summary>
    /// Whether a straight line between two points of the plan stays inside the room: no wall crosses it (touching a
    /// corner or ending on a wall doesn't count). Things behind an inner corner are hidden from the camera so.
    /// </summary>
    /// <param name="from">One end, such as the camera.</param>
    /// <param name="to">The other end, such as a point on an object.</param>
    public bool Sees(OutlinePoint from, OutlinePoint to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        const double margin = 0.5;
        var (dx, dy) = (to.X - from.X, to.Y - from.Y);

        foreach (var wall in Walls)
        {
            double t, along, low, high;

            if (wall.Dx == 0)
            {
                if (dx == 0)
                {
                    continue;
                }

                t = (wall.Start.X - from.X) / dx;
                along = from.Y + t * dy;
                (low, high) = (Math.Min(wall.Start.Y, wall.End.Y), Math.Max(wall.Start.Y, wall.End.Y));
            }
            else
            {
                if (dy == 0)
                {
                    continue;
                }

                t = (wall.Start.Y - from.Y) / dy;
                along = from.X + t * dx;
                (low, high) = (Math.Min(wall.Start.X, wall.End.X), Math.Max(wall.Start.X, wall.End.X));
            }

            var length = Math.Sqrt(dx * dx + dy * dy);
            var inside = t * length > margin && (1 - t) * length > margin;

            if (inside && along > low + margin && along < high - margin)
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public bool Equals(RoomOutline? other)
    {
        return other is not null && Points.SequenceEqual(other.Points);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return Equals(obj as RoomOutline);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return Points.Aggregate(0, HashCode.Combine);
    }

    /// <summary>Whether corners make a valid outline (see the constructor).</summary>
    /// <param name="points">The corners.</param>
    private static bool IsValid(List<OutlinePoint> points)
    {
        if (points.Count < 4 || points.Count > Constants.Plans.MaxCorners || points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)))
        {
            return false;
        }

        if (points.Any(p => p.X != Math.Round(p.X) || p.Y != Math.Round(p.Y)))
        {
            return false;
        }

        var (minX, minY) = (points.Min(p => p.X), points.Min(p => p.Y));
        var (maxX, maxY) = (points.Max(p => p.X), points.Max(p => p.Y));
        var (min, max) = (Constants.Plans.MinWallMetres * 100, Constants.Plans.MaxWallMetres * 100);

        if (minX != 0 || minY != 0 || maxX < min || maxX > max || maxY < min || maxY > max)
        {
            return false;
        }

        // It starts at the top-left corner: the topmost row's leftmost point.
        if (points[0] != points.Where(p => p.Y == 0).MinBy(p => p.X))
        {
            return false;
        }

        var walls = points.Select((p, i) => new OutlineWall(p, points[(i + 1) % points.Count])).ToList();

        for (var i = 0; i < walls.Count; i++)
        {
            var (wall, next) = (walls[i], walls[(i + 1) % walls.Count]);
            // Each wall runs straight across or down, at least a little, and turns a right angle into the next.
            var straight = (wall.Dx == 0) != (wall.Dy == 0);

            if (!straight || wall.Length < Constants.Plans.MinWallSegmentCentimetres || (wall.Dx == 0) == (next.Dx == 0))
            {
                return false;
            }
        }

        // Clockwise on the plan (y down): the shoelace sum is positive.
        var area = points.Select((p, i) => p.X * points[(i + 1) % points.Count].Y - points[(i + 1) % points.Count].X * p.Y).Sum();

        if (area <= 0)
        {
            return false;
        }

        // No two walls that don't meet at a corner touch or cross.
        for (var i = 0; i < walls.Count; i++)
        {
            for (var j = i + 2; j < walls.Count; j++)
            {
                if (i == 0 && j == walls.Count - 1)
                {
                    continue;
                }

                if (Touch(walls[i], walls[j]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Whether two straight walls (each across or down the plan) touch or cross.</summary>
    /// <param name="a">One wall.</param>
    /// <param name="b">The other.</param>
    private static bool Touch(OutlineWall a, OutlineWall b)
    {
        var (aMinX, aMaxX) = (Math.Min(a.Start.X, a.End.X), Math.Max(a.Start.X, a.End.X));
        var (aMinY, aMaxY) = (Math.Min(a.Start.Y, a.End.Y), Math.Max(a.Start.Y, a.End.Y));
        var (bMinX, bMaxX) = (Math.Min(b.Start.X, b.End.X), Math.Max(b.Start.X, b.End.X));
        var (bMinY, bMaxY) = (Math.Min(b.Start.Y, b.End.Y), Math.Max(b.Start.Y, b.End.Y));

        return aMinX <= bMaxX && bMinX <= aMaxX && aMinY <= bMaxY && bMinY <= aMaxY;
    }

    /// <summary>Whether a value lies between two others, whichever way round.</summary>
    /// <param name="value">The value.</param>
    /// <param name="a">One end.</param>
    /// <param name="b">The other.</param>
    private static bool Between(double value, double a, double b)
    {
        return value >= Math.Min(a, b) && value <= Math.Max(a, b);
    }

    /// <summary>Whether a point lies on a wall.</summary>
    /// <param name="wall">The wall.</param>
    /// <param name="x">Across the plan.</param>
    /// <param name="y">Down the plan.</param>
    private static bool OnSegment(OutlineWall wall, double x, double y)
    {
        return x >= Math.Min(wall.Start.X, wall.End.X) && x <= Math.Max(wall.Start.X, wall.End.X)
            && y >= Math.Min(wall.Start.Y, wall.End.Y) && y <= Math.Max(wall.Start.Y, wall.End.Y);
    }
}
