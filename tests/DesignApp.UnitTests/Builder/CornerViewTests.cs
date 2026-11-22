using DesignApp.Application.Helpers;
using DesignApp.Application.Objects;
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

        plan.Update("", plan.Outline, 2.6, Wall, Floor, FloorType.Wood, new PlanView((int)PlanWall.Back, corner), [plant, sofa], Now);

        return plan;
    }

    [Fact]
    public void A_corner_view_is_kept_and_an_unknown_one_is_refused()
    {
        var plan = Room(ViewCorner.Left);

        Assert.Equal(ViewCorner.Left, plan.Corner);
        Assert.Throws<DomainException>(() => plan.Update("", plan.Outline, 2.6, Wall, Floor, FloorType.Wood, new PlanView(0, (ViewCorner)7), [], Now));
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
    public void A_spot_view_looks_from_where_the_photo_was_taken_and_is_dropped_with_a_spot_outside_the_walls()
    {
        var plan = Room(ViewCorner.None);

        plan.Update("", plan.Outline, 2.6, Wall, Floor, FloorType.Wood, new PlanView((int)PlanWall.Back, ViewCorner.Spot, new OutlinePoint(120, 300)), plan.Items, Now);
        var prompt = PlanPrompt.Write(plan);

        Assert.Equal((ViewCorner.Spot, new OutlinePoint(120, 300)), (plan.Corner, plan.CameraSpot));
        Assert.Contains("The camera stands 120 cm from the left wall and 300 cm from the far wall at eye height, looking towards the middle of the far wall.",
            prompt, StringComparison.Ordinal);
        // The sofa is ahead and faces the camera; the plant, 170 cm behind the spot, isn't in the photo.
        Assert.Contains("facing the camera", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("plant", prompt, StringComparison.Ordinal);

        plan.Update("", 4, 2.5, 2.6, Wall, Floor, FloorType.Wood, new PlanView((int)PlanWall.Back, ViewCorner.Spot), [], Now);

        // The smaller room leaves the spot outside: the camera looks from the middle again.
        Assert.Equal((ViewCorner.None, (OutlinePoint?)null), (plan.Corner, plan.CameraSpot));
    }

    [Fact]
    public void The_corner_camera_looks_diagonally_through_its_fixed_lens()
    {
        // From the near left corner of a 400 × 500 room (5 cm in from both walls), turned 45° to the right.
        var camera = new PlanCamera(400, 260, new CornerCamera(left: true).Pose(new ViewGeometry(400, 500, 200, 0, 400, null)));

        // The far right corner is ahead; the far left corner is near the picture's left edge, and the near right one its right edge.
        Assert.Empty(camera.Sees([(395, 5, 100)])!);
        Assert.Empty(camera.Sees([(20, 5, 100)])!);
        Assert.Empty(camera.Sees([(395, 480, 100)])!);
        // Behind it, against the near wall to its left, nothing shows.
        Assert.Null(camera.Sees([(2, 499, 100)]));
    }
}
