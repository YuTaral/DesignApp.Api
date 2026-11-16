using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests.Domain;

/// <summary>A built room's outline: which shapes are allowed, how its walls are numbered, and the walls objects sit on.</summary>
public class RoomOutlineTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    /// <summary>An outline from x, y pairs.</summary>
    /// <param name="corners">The corners as x1, y1, x2, y2...</param>
    private static RoomOutline Outline(params double[] corners)
    {
        return new RoomOutline(corners.Chunk(2).Select(c => new OutlinePoint(c[0], c[1])));
    }

    /// <summary>An L-shaped room: 400 wide at the back, 250 wide at the front, 500 long.</summary>
    private static RoomOutline LShape()
    {
        return Outline(0, 0, 400, 0, 400, 300, 250, 300, 250, 500, 0, 500);
    }

    [Fact]
    public void A_rectangle_has_its_four_walls_numbered_back_right_front_left()
    {
        var outline = RoomOutline.Rectangle(4, 5);

        Assert.True(outline.IsRectangle);
        Assert.Equal((4.0, 5.0), (outline.WidthMetres, outline.LengthMetres));
        Assert.Equal([PlanWall.Back, PlanWall.Right, PlanWall.Front, PlanWall.Left], outline.Walls.Select(w => w.Side));
        Assert.Equal([400.0, 500.0, 400.0, 500.0], outline.Walls.Select(w => w.Length));
        // Along a wall runs from its left end seen from inside: the front wall from the right-hand corner.
        Assert.Equal(new OutlinePoint(300, 500), outline.Walls[2].Point(100));
        Assert.Equal((0, -1), (outline.Walls[2].NormalX, outline.Walls[2].NormalY));
        Assert.Equal(outline, RoomOutline.Rectangle(4, 5));
    }

    [Fact]
    public void L_T_and_U_shaped_rooms_are_allowed()
    {
        var l = LShape();
        var t = Outline(0, 0, 600, 0, 600, 200, 400, 200, 400, 500, 200, 500, 200, 200, 0, 200);
        var u = Outline(0, 0, 100, 0, 100, 300, 300, 300, 300, 0, 400, 0, 400, 400, 0, 400);

        Assert.Equal((6, 8, 8), (l.Walls.Count, t.Walls.Count, u.Walls.Count));
        Assert.Equal([PlanWall.Back, PlanWall.Right, PlanWall.Front, PlanWall.Right, PlanWall.Front, PlanWall.Left], l.Walls.Select(w => w.Side));
        Assert.Equal((4.0, 5.0), (l.WidthMetres, l.LengthMetres));
        Assert.True(l.Contains(100, 400));
        Assert.True(l.Contains(400, 100));
        Assert.False(l.Contains(350, 400));
        Assert.False(u.Contains(200, 100));
    }

    [Theory]
    [InlineData(new double[] { 0, 0, 0, 500, 400, 500, 400, 0 })]                                  // counter-clockwise
    [InlineData(new double[] { 400, 0, 400, 500, 0, 500, 0, 0 })]                                  // not from the top-left
    [InlineData(new double[] { 0, 0, 200, 0, 400, 0, 400, 500, 0, 500 })]                          // two walls in a line
    [InlineData(new double[] { 0, 0, 400, 0, 400, 300, 390, 300, 390, 500, 0, 500 })]              // a 10 cm wall
    [InlineData(new double[] { 0, 0, 400, 0, 400, 500, 0, 400 })]                                  // a slanted wall
    [InlineData(new double[] { 0, 0, 3100, 0, 3100, 500, 0, 500 })]                                // over 30 m
    [InlineData(new double[] { 0, 0, 400, 0, 400, 200, -100, 200, -100, 300, 0, 300, 0, 500 })]    // past 0 and odd
    [InlineData(new double[] { 0, 0, 400.5, 0, 400.5, 500, 0, 500 })]                              // not whole centimetres
    [InlineData(new double[] { 0, 0, 400, 0, 400, 200, 100, 200, 100, 100, 300, 100, 300, 500, 0, 500 })] // walls crossing
    public void Other_outlines_are_refused(double[] corners)
    {
        Assert.Equal(Messages.PlanOutline, Assert.Throws<DomainException>(() => Outline(corners)).Message);
    }

    [Fact]
    public void More_than_twenty_corners_are_refused()
    {
        // A staircase down the right-hand side: 11 steps make 24 corners.
        var corners = new List<double> { 0, 0 };
        for (var i = 0; i < 11; i++)
            corners.AddRange([1100 - i * 50, i * 50, 1100 - i * 50, (i + 1) * 50]);
        corners.AddRange([0, 550]);

        Assert.Throws<DomainException>(() => Outline([.. corners]));
    }

    [Fact]
    public void Objects_on_walls_and_the_view_must_be_on_a_wall_of_the_room()
    {
        var plan = RoomPlan.Create(Guid.NewGuid(), "u", "", RoomType.LivingRoom, 4, 5, 2.6, HexColor.Parse("#E8E2D6"), HexColor.Parse("#B89B7A"), FloorType.Wood, AiMode.Fake, Now);
        var (wall, floor, white) = (HexColor.Parse("#E8E2D6"), HexColor.Parse("#B89B7A"), HexColor.Parse("#F4F1EA"));
        PlanItem Window(int on, double x)
        {
            return new PlanItem("window", ItemShape.Window, x, 0, 120, 10, 140, 0, white, on, 90);
        }

        // Wall 3 of the L is the 200 cm step in its right side.
        plan.Update("", LShape(), 2.6, wall, floor, FloorType.Wood, 3, [Window(3, 100)], Now);
        Assert.Equal((6, 3, 200.0, 4.0), (plan.Outline.Walls.Count, plan.View, plan.WallCentimetres(3), plan.Width));

        Assert.Throws<DomainException>(() => plan.Update("", LShape(), 2.6, wall, floor, FloorType.Wood, 3, [Window(3, 180)], Now));
        Assert.Equal(Messages.PlanWallPlacement, Assert.Throws<DomainException>(() => plan.Update("", LShape(), 2.6, wall, floor, FloorType.Wood, 0, [Window(6, 100)], Now)).Message);
        Assert.Equal(Messages.PlanLook, Assert.Throws<DomainException>(() => plan.Update("", LShape(), 2.6, wall, floor, FloorType.Wood, 6, [], Now)).Message);
        Assert.Throws<DomainException>(() => Window(-1, 100));
    }
}
