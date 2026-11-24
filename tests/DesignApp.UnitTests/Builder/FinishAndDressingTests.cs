using DesignApp.Application.Helpers;
using DesignApp.Application.UseCases;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Extensions;
using DesignApp.Domain.Objects;
using DesignApp.Infrastructure.Builder;

using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests.Builder;

/// <summary>
/// A built room's finish (walls, accent wall, ceiling, trim, light and mood), its objects' materials, and how a furnished
/// new room is dressed: curtains, decorations, materials and things hung on its walls.
/// </summary>
public class FinishAndDressingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);
    private static readonly HexColor Wall = HexColor.Parse("#E8E2D6");
    private static readonly HexColor Floor = HexColor.Parse("#B89B7A");
    private static readonly HexColor Grey = HexColor.Parse("#8A8580");
    private static readonly HexColor Brick = HexColor.Parse("#B5654A");
    private readonly EmbeddedObjectCatalog _catalog = new();

    /// <summary>A 4 × 5 m living room looking at its back wall, with a velvet sofa facing the camera.</summary>
    /// <param name="finish">The room's finish.</param>
    private static RoomPlan Room(RoomFinish finish)
    {
        var plan = RoomPlan.Create(Guid.CreateVersion7(Now), "user-1", "", RoomType.LivingRoom, 4, 5, 2.6, Wall, Floor, FloorType.Wood, AiMode.Fake, Now);
        var sofa = new PlanItem("three-seat sofa", ItemShape.Sofa, 200, 100, 220, 95, 85, 0, Grey, material: "velvet");

        plan.Update("", plan.Outline, 2.6, Wall, Floor, FloorType.Wood, (int)PlanWall.Back, [sofa], Now, finish);

        return plan;
    }

    /// <summary>A finish with brick on one wall, a dark ceiling, black trim, a chandelier, in the evening.</summary>
    /// <param name="accentWall">The brick wall's number.</param>
    private static RoomFinish Moody(int? accentWall)
    {
        return new RoomFinish(WallFinish.Plaster, accentWall, Brick, WallFinish.Brick, HexColor.Parse("#3A3A3A"), HexColor.Parse("#1E1E1E"),
            CeilingLight.Chandelier, TimeOfDay.Evening);
    }

    [Fact]
    public void A_finish_is_kept_and_an_unknown_choice_or_wall_is_refused()
    {
        var plan = Room(Moody((int)PlanWall.Left));

        Assert.Equal(Moody((int)PlanWall.Left), plan.Finish);
        Assert.Equal(RoomFinish.Default, RoomPlan.Create(Guid.CreateVersion7(Now), "user-1", "", RoomType.Bedroom, 4, 4, 2.6, Wall, Floor, FloorType.Wood,
            AiMode.Fake, Now).Finish);
        Assert.Throws<DomainException>(() => new RoomFinish((WallFinish)99, null, Brick, WallFinish.Paint, Wall, Wall, CeilingLight.None, TimeOfDay.Midday));
        Assert.Throws<DomainException>(() => new RoomFinish(WallFinish.Paint, -1, Brick, WallFinish.Paint, Wall, Wall, CeilingLight.None, TimeOfDay.Midday));
        // A rectangle has four walls: a fifth can't be the accent wall.
        Assert.Throws<DomainException>(() => plan.Update("", plan.Outline, 2.6, Wall, Floor, FloorType.Wood, 0, [], Now, Moody(4)));
    }

    [Fact]
    public void Saving_without_a_finish_keeps_it_and_drops_an_accent_wall_the_new_outline_lacks()
    {
        var plan = Room(Moody((int)PlanWall.Left));
        var lShape = new RoomOutline([new(0, 0), new(400, 0), new(400, 300), new(250, 300), new(250, 500), new(0, 500)]);

        plan.Update("", plan.Outline, 2.6, Wall, Floor, FloorType.Wood, 0, [], Now);
        var kept = plan.Finish;

        plan.Update("", lShape, 2.6, Wall, Floor, FloorType.Wood, 0, [], Now, Moody(5));
        plan.Update("", RoomPlan.RectangleOutline(4, 5), 2.6, Wall, Floor, FloorType.Wood, 0, [], Now);
        var shrunk = plan.Finish;

        Assert.Equal(Moody((int)PlanWall.Left), kept);
        Assert.Null(shrunk.AccentWall);
        Assert.Equal(TimeOfDay.Evening, shrunk.TimeOfDay);
    }

    [Fact]
    public void The_photo_prompt_says_the_material_finish_accent_wall_ceiling_light_and_mood()
    {
        var moody = PlanPrompt.Write(Room(Moody((int)PlanWall.Back)));
        var behind = PlanPrompt.Write(Room(Moody((int)PlanWall.Front)));
        var plain = PlanPrompt.Write(Room(RoomFinish.Default));

        Assert.Contains("velvet three-seat sofa", moody, StringComparison.Ordinal);
        Assert.Contains("The walls are finished in softly mottled limewash plaster in", moody, StringComparison.Ordinal);
        Assert.Contains("except the far wall, which is exposed brick in", moody, StringComparison.Ordinal);
        Assert.Contains("(#3A3A3A) with a chandelier", moody, StringComparison.Ordinal);
        Assert.Contains("the skirting boards are black (#1E1E1E)", moody, StringComparison.Ordinal);
        Assert.Contains("It is evening", moody, StringComparison.Ordinal);
        // The wall behind the camera isn't in the photo, so its brick isn't mentioned.
        Assert.DoesNotContain("exposed brick", behind, StringComparison.Ordinal);
        Assert.Contains("The walls are painted", plain, StringComparison.Ordinal);
        Assert.Contains("a pendant lamp", plain, StringComparison.Ordinal);
        Assert.Contains("Bright, even daylight", plain, StringComparison.Ordinal);
    }

    [Fact]
    public void A_material_is_trimmed_kept_and_limited()
    {
        var sofa = new PlanItem("sofa", ItemShape.Sofa, 100, 100, 200, 90, 85, 0, Grey, material: "  bouclé ");

        Assert.Equal("bouclé", sofa.Material);
        Assert.Equal("", new PlanItem("sofa", ItemShape.Sofa, 100, 100, 200, 90, 85, 0, Grey).Material);
        Assert.Throws<DomainException>(() => new PlanItem("sofa", ItemShape.Sofa, 100, 100, 200, 90, 85, 0, Grey, material: new string('x', Plans.MaxMaterialLength + 1)));
    }

    [Fact]
    public void Catalog_objects_offer_materials_and_start_in_one_of_them()
    {
        Assert.All(Enum.GetValues<RoomType>(), type =>
        {
            var objects = _catalog.ForRoom(type);

            // Nearly everything has materials to choose from; a TV, pinboard or wall stickers may not.
            Assert.True(objects.Count(o => o.Materials is { Count: > 0 }) >= objects.Count - 2, type.ToString());
            Assert.All(objects.Where(o => o.Materials is { Count: > 0 }), o => Assert.Contains(o.Material, o.Materials!));
            Assert.All(objects, o => Assert.All(o.Dressing ?? [], name => Assert.Contains(name, o.Decorations!)));
            // A material never names a color (the object's color does, and "a charcoal white frame" contradicts itself);
            // white oak and white marble are kinds of wood and stone.
            Assert.All(objects.SelectMany(o => o.Materials ?? []).Except(["white oak", "white marble"]),
                name => Assert.DoesNotMatch(@"\b(white|black|grey|gray|cream)\b", name));
        });
        var sofa = _catalog.ForRoom(RoomType.LivingRoom).Single(o => o.Key == "sofa");

        Assert.Contains("velvet", sofa.Materials!);
        Assert.Equal("linen", sofa.Material);
    }

    [Fact]
    public void A_furnished_room_dresses_its_windows_and_furniture_and_hangs_things_on_its_walls()
    {
        Assert.All(Enum.GetValues<RoomType>(), type =>
        {
            var items = StarterLayout.For(type, 4, 5, 2.6, _catalog.ForRoom(type));
            var wet = type is RoomType.Kitchen or RoomType.Bathroom;

            Assert.All(items.Where(i => i.Shape == ItemShape.Window), w => Assert.Equal([wet ? "roller blind" : "sheer curtains"], w.Decorations.Names));
            Assert.Contains(items, i => i.Shape.IsMounted());
            Assert.True(items.Count(i => !i.Decorations.IsEmpty) >= 4, $"{type}: only {items.Count(i => !i.Decorations.IsEmpty)} dressed");
            Assert.True(items.Count(i => i.Material.Length > 0) >= items.Count - 2, $"{type}: {string.Join(", ", items.Where(i => i.Material.Length == 0).Select(i => i.Label))}");
        });
        var living = StarterLayout.For(RoomType.LivingRoom, 4, 5, 2.6, _catalog.ForRoom(RoomType.LivingRoom));
        var kitchen = StarterLayout.For(RoomType.Kitchen, 4, 5, 2.6, _catalog.ForRoom(RoomType.Kitchen));
        var bathroom = StarterLayout.For(RoomType.Bathroom, 4, 5, 2.6, _catalog.ForRoom(RoomType.Bathroom));
        var bedroom = StarterLayout.For(RoomType.Bedroom, 4, 5, 2.6, _catalog.ForRoom(RoomType.Bedroom));
        var bed = bedroom.Single(i => i.Shape == ItemShape.Bed);

        Assert.Contains(living, i => i.Label == "wall TV" && i.Wall == (int)PlanWall.Right);
        Assert.Contains(living, i => i.Label == "set of three framed prints");
        Assert.Contains(living, i => i.Label == "three-seat sofa" && i.Decorations.Names.Contains("cushions"));
        Assert.Contains(kitchen, i => i.Label.StartsWith("sink base cabinet", StringComparison.Ordinal));
        Assert.Contains(kitchen, i => i.Label == "integrated dishwasher");
        Assert.Contains(kitchen, i => i.Label == "upper cabinets");
        Assert.Contains(bathroom, i => i.Label == "mirror cabinet");
        Assert.Contains(bedroom, i => i.Label == "set of three framed prints" && i.Wall == (int)PlanWall.Right && i.Elevation > bed.Height);
    }

    [Fact]
    public void Hung_things_keep_clear_of_windows_doors_each_other_furniture_and_the_ceiling()
    {
        foreach (var type in Enum.GetValues<RoomType>())
        {
            foreach (var (width, length, height) in new[] { (2.6, 2.4, 2.4), (3.5, 4.0, 2.5), (4.0, 5.0, 2.6), (6.0, 6.0, 3.0) })
            {
                var items = StarterLayout.For(type, width, length, height, _catalog.ForRoom(type));
                var onWalls = items.Where(i => i.Wall is not null).ToList();
                var hung = onWalls.Where(i => i.Shape.IsMounted()).ToList();

                Assert.All(hung, h =>
                {
                    Assert.True(h.Elevation + h.Height <= height * 100 - 10, $"{type}: {h.Label} reaches the ceiling");
                    Assert.DoesNotContain(onWalls, o => !ReferenceEquals(o, h) && o.Wall == h.Wall
                        && h.X - h.Width / 2 < o.X + o.Width / 2 && o.X - o.Width / 2 < h.X + h.Width / 2
                        && h.Elevation < o.Elevation + o.Height && o.Elevation < h.Elevation + h.Height);
                });
            }
        }
    }

    [Fact]
    public async Task A_furnished_room_starts_with_a_finish_for_its_type()
    {
        var create = new CreatePlan(new FakePlans(), new FixedUser("user-1"), new FixedClock(Now), _catalog, new PlanDesigns(new FakeDesigns(), new FakePlans(), new FixedClock(Now)));

        var bathroom = (await create.HandleAsync("", RoomType.Bathroom, 3, 3, 2.5, Wall, Floor, FloorType.Tiles, AiMode.Fake, true, null, null, null, CancellationToken.None)).Value;
        var bedroom = (await create.HandleAsync("", RoomType.Bedroom, 4, 4, 2.6, Wall, Floor, FloorType.Wood, AiMode.Fake, true, null, null, null, CancellationToken.None)).Value;
        var empty = (await create.HandleAsync("", RoomType.Bathroom, 3, 3, 2.5, Wall, Floor, FloorType.Tiles, AiMode.Fake, false, null, null, null, CancellationToken.None)).Value;
        var chosen = (await create.HandleAsync("", RoomType.Bedroom, 4, 4, 2.6, Wall, Floor, FloorType.Wood, AiMode.Fake, true, Moody(null), null, null, CancellationToken.None)).Value;

        Assert.Equal(WallFinish.Tiles, bathroom.Finish.Walls);
        Assert.Equal(CeilingLight.Spotlights, bathroom.Finish.CeilingLight);
        Assert.Equal((int)PlanWall.Right, bedroom.Finish.AccentWall);
        Assert.Equal(RoomFinish.Default, empty.Finish);
        Assert.Equal(Moody(null), chosen.Finish);
    }
}
