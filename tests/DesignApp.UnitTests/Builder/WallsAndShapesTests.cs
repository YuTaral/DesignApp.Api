using DesignApp.Application.Helpers;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;
using DesignApp.Infrastructure.Database;

using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests.Builder;

/// <summary>
/// Things hung on walls (paintings, mirrors, upper cabinets...) and rooms whose walls bend: their rules, how they are
/// stored, and how the photo prompt describes them.
/// </summary>
public class WallsAndShapesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 20, 0, 0, TimeSpan.Zero);
    private static readonly HexColor Wall = HexColor.Parse("#E8E2D6");
    private static readonly HexColor Floor = HexColor.Parse("#B89B7A");
    private static readonly HexColor White = HexColor.Parse("#F4F1EA");
    private static readonly HexColor Grey = HexColor.Parse("#8A8580");

    /// <summary>A 4 × 5 m living room.</summary>
    private static RoomPlan NewPlan()
    {
        return RoomPlan.Create(Guid.CreateVersion7(Now), "user-1", "", RoomType.LivingRoom, 4, 5, 2.6, Wall, Floor, FloorType.Wood, AiMode.Fake, Now);
    }

    /// <summary>An L-shaped room: 400 wide at the back, 250 at the front, 500 long; its walls 0 to 5 clockwise from the top-left.</summary>
    private static RoomOutline LShape()
    {
        return new RoomOutline([new(0, 0), new(400, 0), new(400, 300), new(250, 300), new(250, 500), new(0, 500)]);
    }

    /// <summary>A painting, 80 × 60 cm, its bottom 130 cm up.</summary>
    /// <param name="wall">Its wall.</param>
    /// <param name="along">Its middle along the wall.</param>
    private static PlanItem Painting(int wall, double along)
    {
        return new PlanItem("painting", ItemShape.Picture, along, 0, 80, 3, 60, 0, HexColor.Parse("#2F2C2A"), wall, 130);
    }

    [Fact]
    public void Hung_things_go_on_a_wall_at_a_height_and_stick_out_only_so_far()
    {
        Assert.Equal(Messages.PlanWallPlacement,
            Assert.Throws<DomainException>(() => new PlanItem("painting", ItemShape.Picture, 100, 100, 80, 3, 60, 0, White)).Message);
        Assert.Equal(Messages.PlanMountedDepth,
            Assert.Throws<DomainException>(() => new PlanItem("deep cabinet", ItemShape.WallBox, 100, 0, 80, 80, 70, 0, White, 0, 145)).Message);
        // A door still stands on the floor; a cabinet hangs at any height.
        Assert.Throws<DomainException>(() => new PlanItem("door", ItemShape.Door, 100, 0, 90, 5, 210, 0, White, 0, 10));
        var cabinet = new PlanItem("upper cabinets", ItemShape.WallBox, 100, 0, 120, 35, 70, 0, White, 0, 145);
        var plan = NewPlan();

        plan.Update("", 4, 5, 2.6, Wall, Floor, FloorType.Wood, PlanWall.Back, [cabinet, Painting(1, 250)], Now);
        Assert.Equal(2, plan.Items.Count);
        // Under the ceiling: a painting hung at 220 cm is 20 cm too high in a 260 cm room.
        var tooHigh = new PlanItem("painting", ItemShape.Picture, 200, 0, 80, 3, 60, 0, White, 0, 220);
        Assert.Throws<DomainException>(() => plan.Update("", 4, 5, 2.6, Wall, Floor, FloorType.Wood, PlanWall.Back, [tooHigh], Now));

        var json = (string)PlanItemsJson.Converter.ConvertToProvider(plan.Items)!;
        Assert.Equal(plan.Items, (IReadOnlyList<PlanItem>)PlanItemsJson.Converter.ConvertFromProvider(json)!);
        Assert.Contains("\"shape\":\"WallBox\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void The_prompt_lists_what_hangs_on_the_walls_in_view()
    {
        var plan = NewPlan();
        var cabinets = new PlanItem("upper cabinets", ItemShape.WallBox, 400, 0, 120, 35, 70, 0, White, (int)PlanWall.Left, 145);
        var mirror = new PlanItem("mirror", ItemShape.Picture, 200, 0, 60, 3, 90, 0, White, (int)PlanWall.Front, 110);
        plan.Update("", 4, 5, 2.6, Wall, Floor, FloorType.Wood, PlanWall.Back, [Painting((int)PlanWall.Back, 200), cabinets, mirror], Now);

        var text = PlanPrompt.Write(plan, PhotoStyle.Modern);

        Assert.Contains("Hung on the walls, and nothing else: ", text, StringComparison.Ordinal);
        Assert.Contains("painting in a charcoal (#2F2C2A) frame, on the far wall, its middle 200 cm from the left wall, 80 cm wide and 60 cm high, its bottom 130 cm above the floor, sticking out 3 cm", text, StringComparison.Ordinal);
        Assert.Contains("upper cabinets, on the left wall, its middle 100 cm from the far wall, 120 cm wide and 70 cm high, its bottom 145 cm above the floor, sticking out 35 cm", text, StringComparison.Ordinal);
        // The mirror hangs behind the camera; hung things keep their colors like the rest.
        Assert.DoesNotContain("mirror", text, StringComparison.Ordinal);
        Assert.Contains("Every object keeps exactly the color", text, StringComparison.Ordinal);
        // A picture's color is its frame's, so the image model doesn't paint the canvas in it.
        Assert.Contains("the painting's frame stays", text, StringComparison.Ordinal);
        Assert.DoesNotContain("not a plain rectangle", text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_L_shaped_room_is_described_from_where_the_camera_stands_and_its_corner_hides_what_is_behind_it()
    {
        var plan = NewPlan();
        // Wall 3 is the step in the right-hand side, 250 cm from the left; wall 1 is the right wall of the back part.
        var stepWindow = new PlanItem("window", ItemShape.Window, 100, 0, 120, 10, 140, 0, White, 3, 90);
        var hiddenWindow = new PlanItem("far side window", ItemShape.Window, 150, 0, 120, 10, 140, 0, White, 1, 90);
        var sofa = new PlanItem("sofa", ItemShape.Sofa, 230, 200, 220, 95, 85, 0, Grey);
        var armchair = new PlanItem("armchair", ItemShape.Chair, 350, 200, 80, 80, 90, 0, Grey);
        plan.Update("", LShape(), 2.6, Wall, Floor, FloorType.Wood, 0, [stepWindow, hiddenWindow, sofa, armchair], Now);

        var text = PlanPrompt.Write(plan);

        Assert.Contains("400 cm wide from the left wall to the right wall, 500 cm deep from the camera to the far wall", text, StringComparison.Ordinal);
        // The step hides the back part's right wall (400 cm from the left) from the camera, so only the step and the left wall are named.
        Assert.Contains("The room is not a plain rectangle; besides the far wall, the walls in view are: " +
            "a right-hand wall 250 cm from the left wall, from 300 to 500 cm from the far wall; a left-hand wall 0 cm from the left wall, from 0 to 500 cm from the far wall.", text, StringComparison.Ordinal);
        Assert.Contains("window, on the right-hand wall 250 cm from the left wall, its middle 400 cm from the far wall", text, StringComparison.Ordinal);
        Assert.Contains("partly hidden behind a corner of the room", text, StringComparison.Ordinal);
        // The camera stands in the front part: the corner hides the armchair and the window behind it.
        Assert.DoesNotContain("armchair", text, StringComparison.Ordinal);
        Assert.DoesNotContain("far side window", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_wall_across_the_room_and_what_a_straight_line_crosses()
    {
        var l = LShape();

        Assert.Equal((4, 500.0), l.Facing(0));
        Assert.Equal((5, 250.0), l.Facing(3));
        Assert.Equal((0, 300.0), l.Facing(2));
        Assert.True(l.Sees(new OutlinePoint(200, 495), new OutlinePoint(100, 10)));
        Assert.False(l.Sees(new OutlinePoint(200, 495), new OutlinePoint(350, 200)));
        // Ending on a wall or passing through a corner doesn't count.
        Assert.True(l.Sees(new OutlinePoint(100, 400), new OutlinePoint(250, 400)));
        Assert.True(l.Sees(new OutlinePoint(150, 400), new OutlinePoint(350, 200)));
    }
}
