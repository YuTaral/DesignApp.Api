using DesignApp.Application.Helpers;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests.Builder;

/// <summary>
/// Areas of the floor and walls in their own material and color: what they may be made of, and how the photo prompt
/// words them (as surfaces, never as furniture).
/// </summary>
public class ZoneTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);
    private static readonly HexColor Wall = HexColor.Parse("#E8E2D6");
    private static readonly HexColor Floor = HexColor.Parse("#B89B7A");
    private static readonly HexColor White = HexColor.Parse("#FFFFFF");
    private static readonly HexColor Terracotta = HexColor.Parse("#B5543C");

    /// <summary>A floor area of tiles.</summary>
    private static PlanItem Tiles()
    {
        return new PlanItem("floor area", ItemShape.FloorZone, 300, 100, 160, 120, 1, 0, White, material: nameof(FloorType.Tiles));
    }

    /// <summary>A brick area on a wall, from the floor to 100 cm up.</summary>
    /// <param name="wall">The wall.</param>
    private static PlanItem Brick(int wall)
    {
        return new PlanItem("wall area", ItemShape.WallZone, 200, 0, 180, 1, 100, 0, Terracotta, wall, 0, material: nameof(WallFinish.Brick));
    }

    [Fact]
    public void A_zone_is_made_of_its_kinds_materials_and_carries_nothing()
    {
        var tiles = Tiles();
        var brick = Brick((int)PlanWall.Back);

        Assert.Equal((ItemShape.FloorZone, "Tiles"), (tiles.Shape, tiles.Material));
        Assert.Equal((0, 0.0), (brick.Wall, brick.Elevation));
        Assert.Throws<DomainException>(() => new PlanItem("floor area", ItemShape.FloorZone, 300, 100, 160, 120, 1, 0, White, material: "velvet"));
        Assert.Throws<DomainException>(() => new PlanItem("floor area", ItemShape.FloorZone, 300, 100, 160, 120, 1, 0, White, material: nameof(WallFinish.Brick)));
        Assert.Throws<DomainException>(() => new PlanItem("floor area", ItemShape.FloorZone, 300, 100, 160, 120, 1, 0, White,
            decorations: new ItemDecorations(["cushions"]), material: nameof(FloorType.Tiles)));
        // A wall area belongs to a wall.
        Assert.Throws<DomainException>(() => new PlanItem("wall area", ItemShape.WallZone, 200, 0, 180, 1, 100, 0, Terracotta, material: nameof(WallFinish.Brick)));
    }

    [Fact]
    public void The_photo_prompt_words_zones_as_parts_of_the_floor_and_walls_in_view()
    {
        var plan = RoomPlan.Create(Guid.CreateVersion7(Now), "user-1", "", RoomType.LivingRoom, 4, 5, 2.6, Wall, Floor, FloorType.Wood, AiMode.Fake, Now);
        var sofa = new PlanItem("sofa", ItemShape.Sofa, 200, 300, 220, 95, 85, 0, HexColor.Parse("#8A8580"));

        // The camera looks at the back wall: the front wall is behind it.
        plan.Update("", plan.Outline, 2.6, Wall, Floor, FloorType.Wood, new PlanView((int)PlanWall.Back, ViewCorner.None),
            [sofa, Tiles(), Brick((int)PlanWall.Back), Brick((int)PlanWall.Front)], Now);
        var prompt = PlanPrompt.Write(plan);

        Assert.Contains("Part of the floor is ceramic floor tiles in white (#FFFFFF): an area 160 cm wide and 120 cm deep", prompt, StringComparison.Ordinal);
        Assert.Contains("Part of a wall, on the far wall", prompt, StringComparison.Ordinal);
        Assert.Contains("is exposed brick in terracotta (#B5543C): an area 180 cm wide from 0 cm to 100 cm above the floor", prompt, StringComparison.Ordinal);
        // One wall area only (the front wall's is behind the camera), and neither area is furniture.
        Assert.Single(prompt.Split("Part of a wall", StringSplitOptions.None).Skip(1));
        Assert.DoesNotContain("floor area", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("wall area", prompt, StringComparison.Ordinal);
        Assert.Contains("1. warm grey (#8A8580) sofa", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("2. ", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void The_360_prompt_lists_zones_as_areas_not_objects()
    {
        var plan = RoomPlan.Create(Guid.CreateVersion7(Now), "user-1", "", RoomType.LivingRoom, 4, 5, 2.6, Wall, Floor, FloorType.Wood, AiMode.Fake, Now);

        plan.Update("", 4, 5, 2.6, Wall, Floor, FloorType.Wood, PlanWall.Back, [new PlanItem("sofa", ItemShape.Sofa, 200, 300, 220, 95, 85, 0, Wall), Tiles()], Now);
        var prompt = PanoramaPrompt.Write(plan);

        Assert.Contains("an area of the floor, 160 by 120 cm, in ceramic floor tiles in white (#FFFFFF)", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("2. ", prompt, StringComparison.Ordinal);
    }
}
