using DesignApp.Application.Helpers;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests.Builder;

/// <summary>
/// The corner view: the camera in a near corner of the room, turned to look diagonally across it. What it sees, and how
/// the photo prompt says so.
/// </summary>
public class CornerViewTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);
    private static readonly HexColor Wall = HexColor.Parse("#E8E2D6");
    private static readonly HexColor Floor = HexColor.Parse("#B89B7A");
    private static readonly HexColor Grey = HexColor.Parse("#8A8580");

    /// <summary>A 4 × 5 m living room looking at its back wall, with a plant in the near right corner and a sofa facing the front.</summary>
    /// <param name="corner">Where the camera stands.</param>
    private static RoomPlan Room(ViewCorner corner)
    {
        var plan = RoomPlan.Create(Guid.CreateVersion7(Now), "user-1", "", RoomType.LivingRoom, 4, 5, 2.6, Wall, Floor, FloorType.Wood, AiMode.Fake, Now);
        var plant = new PlanItem("plant", ItemShape.Plant, 370, 470, 40, 40, 120, 0, Grey);
        var sofa = new PlanItem("sofa", ItemShape.Box, 200, 100, 200, 90, 80, 0, Grey);
        plan.Update("", plan.Outline, 2.6, Wall, Floor, FloorType.Wood, (int)PlanWall.Back, [plant, sofa], Now, corner);
        return plan;
    }

    [Fact]
    public void A_corner_view_is_kept_and_an_unknown_one_is_refused()
    {
        var plan = Room(ViewCorner.Left);

        Assert.Equal(ViewCorner.Left, plan.Corner);
        Assert.Throws<DomainException>(() => plan.Update("", plan.Outline, 2.6, Wall, Floor, FloorType.Wood, 0, [], Now, (ViewCorner)7));
    }

    [Fact]
    public void The_corner_camera_sees_what_stands_beside_the_straight_camera()
    {
        var straight = PlanPrompt.Write(Room(ViewCorner.None));
        var left = PlanPrompt.Write(Room(ViewCorner.Left));
        var right = PlanPrompt.Write(Room(ViewCorner.Right));

        // Straight on, the plant 25 cm in front of the camera's wall and 170 cm to its right is beside the camera.
        Assert.DoesNotContain("plant", straight, StringComparison.Ordinal);
        Assert.Contains("plant", left, StringComparison.Ordinal);
        Assert.Contains("plant", right, StringComparison.Ordinal);
    }

    [Fact]
    public void A_corner_prompt_says_where_the_camera_stands_and_names_walls_not_the_camera()
    {
        var straight = PlanPrompt.Write(Room(ViewCorner.None));
        var left = PlanPrompt.Write(Room(ViewCorner.Left));
        var right = PlanPrompt.Write(Room(ViewCorner.Right));

        Assert.Contains("The camera stands at the middle of the near wall at eye height, looking straight at the far wall.", straight, StringComparison.Ordinal);
        Assert.Contains("sofa", straight, StringComparison.Ordinal);
        Assert.Contains("facing the camera", straight, StringComparison.Ordinal);
        Assert.Contains("The camera stands in the near left corner at eye height, looking diagonally across the room towards the far right corner, " +
            "so the far wall fills the left of the photo and the right wall its right.", left, StringComparison.Ordinal);
        Assert.Contains("near right corner", right, StringComparison.Ordinal);
        Assert.Contains("the left wall fills the left of the photo and the far wall its right", right, StringComparison.Ordinal);
        Assert.Contains("facing the near wall", left, StringComparison.Ordinal);
        Assert.DoesNotContain("facing the camera", left, StringComparison.Ordinal);
    }

    [Fact]
    public void The_corner_camera_looks_diagonally_through_its_fixed_lens()
    {
        // From the near left corner of a 400 × 500 room (5 cm in from both walls), turned 45° to the right.
        var camera = new PlanCamera(400, 500, 260, 5, ViewCorner.Left);

        // The far right corner is ahead; the far left corner is near the picture's left edge, and the near right one its right edge.
        Assert.Empty(camera.Sees([(395, 5, 100)])!);
        Assert.Empty(camera.Sees([(20, 5, 100)])!);
        Assert.Empty(camera.Sees([(395, 480, 100)])!);
        // Behind it, against the near wall to its left, nothing shows.
        Assert.Null(camera.Sees([(2, 499, 100)]));
    }
}
