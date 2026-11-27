using DesignApp.Application.Helpers;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Extensions;
using DesignApp.Domain.Objects;
using DesignApp.Infrastructure.Database;

using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests.Builder;

/// <summary>
/// How a built room is fitted: its cabinets' fronts and handles, the hardware, the kitchen sink and tap, the vanity's
/// basin and the tile pattern; how they are stored, and that the photo prompt words only what the room has.
/// </summary>
public class FittingsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);
    private static readonly HexColor White = HexColor.Parse("#F4F1EA");
    private static readonly RoomFittings Fancy = new(CabinetFronts.Shaker, true, HandleStyle.Knob, Hardware.BrushedBrass, KitchenSink.Farmhouse, KitchenTap.Bridge,
        Basin.Vessel, TilePattern.Herringbone);

    /// <summary>A 4 × 5 m room of a type looking at its back wall, with a window on it, fitted as given, its floor and walls as given.</summary>
    /// <param name="type">The room's type.</param>
    /// <param name="fittings">How it is fitted.</param>
    /// <param name="floor">Its floor.</param>
    /// <param name="items">Its objects.</param>
    private static RoomPlan Room(RoomType type, RoomFittings fittings, FloorType floor, params PlanItem[] items)
    {
        var plan = RoomPlan.Create(Guid.CreateVersion7(Now), "user-1", "", type, 4, 5, 2.6, White, White, floor, AiMode.Fake, Now);
        var window = new PlanItem("window", ItemShape.Window, 200, 0, 120, 10, 140, 0, White, (int)PlanWall.Back, 90);
        var plain = RoomFinish.Default;
        var finish = new RoomFinish(plain.Walls, [], plain.CeilingColor, plain.TrimColor, plain.TimeOfDay, fittings: fittings);

        plan.Update("", plan.Outline, 2.6, White, White, floor, (int)PlanWall.Back, [window, .. items], Now, finish);

        return plan;
    }

    /// <summary>A kitchen's sink base unit against the back wall.</summary>
    private static PlanItem SinkUnit()
    {
        return new PlanItem("sink base unit", ItemShape.Box, 300, 30, 80, 60, 90, 0, White, material: "matt lacquer");
    }

    /// <summary>Upper cabinets on the back wall.</summary>
    private static PlanItem Uppers()
    {
        return new PlanItem("upper cabinets", ItemShape.WallBox, 100, 0, 120, 35, 70, 0, White, (int)PlanWall.Back, 150, material: "matt lacquer");
    }

    /// <summary>A bathroom vanity against the left wall.</summary>
    private static PlanItem Vanity()
    {
        return new PlanItem("vanity unit", ItemShape.Box, 30, 250, 80, 45, 85, 90, White, material: "oak veneer");
    }

    [Fact]
    public void Fittings_refuse_unknown_choices_and_a_room_knows_what_it_has()
    {
        var kitchen = Room(RoomType.Kitchen, Fancy, FloorType.Wood, SinkUnit(), Uppers());
        var bathroom = Room(RoomType.Bathroom, Fancy, FloorType.Tiles, Vanity());
        var lounge = Room(RoomType.LivingRoom, Fancy, FloorType.Wood, new PlanItem("sofa", ItemShape.Sofa, 200, 250, 200, 90, 85, 0, White));

        Assert.Throws<DomainException>(() => (RoomFittings.Default with { Handles = (HandleStyle)9 }).Checked());
        Assert.Throws<DomainException>(() => new RoomFinish(WallFinish.Paint, [], White, White, TimeOfDay.Midday, fittings: RoomFittings.Default with { Tiles = (TilePattern)9 }));
        Assert.Equal((true, true, true, false, false), (kitchen.HasCabinets(), kitchen.HasUpperCabinets(), kitchen.HasSink(), kitchen.HasVanity(), kitchen.UsesTiles()));
        Assert.Equal((true, false, false, true, true), (bathroom.HasCabinets(), bathroom.HasUpperCabinets(), bathroom.HasSink(), bathroom.HasVanity(), bathroom.UsesTiles()));
        Assert.Equal((false, false), (lounge.HasCabinets(), lounge.UsesTiles()));
        Assert.Equal((false, true, false), bathroom.Tiled());
        // A mirror cabinet's front is its mirror: not fitted cabinetry.
        Assert.False(new PlanItem("mirror cabinet", ItemShape.WallBox, 100, 0, 60, 15, 70, 0, White, (int)PlanWall.Back, 120).IsCabinet());
    }

    [Fact]
    public void The_prompt_words_the_cabinets_sink_basin_and_tiles_only_where_the_room_has_them()
    {
        var kitchen = PlanPrompt.Write(Room(RoomType.Kitchen, Fancy, FloorType.Wood, SinkUnit(), Uppers()));
        var bathroom = PlanPrompt.Write(Room(RoomType.Bathroom, Fancy, FloorType.Tiles, Vanity()));
        var lounge = PlanPrompt.Write(Room(RoomType.LivingRoom, Fancy, FloorType.Wood));
        var plainKitchen = PlanPrompt.Write(Room(RoomType.Kitchen, RoomFittings.Default, FloorType.Wood, SinkUnit()));

        Assert.Contains("The cabinets have shaker fronts with round knobs, the upper ones glass-fronted; handles, taps and fittings are brushed brass.", kitchen);
        Assert.Contains("The kitchen sink is a deep farmhouse sink with an apron front with a traditional bridge tap.", kitchen);
        Assert.DoesNotContain("vanity has", kitchen);
        Assert.DoesNotContain("tiles are", kitchen);
        Assert.Contains("The vanity has a vessel basin, a bowl sitting on its top.", bathroom);
        Assert.Contains("The floor tiles are laid in a herringbone pattern: rectangular tiles set at right angles in a zig-zag.", bathroom);
        // Without upper cabinets nothing is glass-fronted; a living room with none of these says none of it.
        Assert.DoesNotContain("glass-fronted", bathroom);
        Assert.DoesNotContain("The cabinets have", lounge);
        Assert.DoesNotContain("tiles are", lounge);
        Assert.Contains("The cabinets have flat slab fronts with slim bar handles; handles, taps and fittings are brushed stainless steel.", plainKitchen);
    }

    [Fact]
    public void Fittings_are_stored_and_a_finish_stored_before_them_reads_as_the_default()
    {
        const string older = """{"walls":"Paint","ownWalls":[],"ceilingColor":"#FAFAF7","trimColor":"#F4F1EA","timeOfDay":"Midday","tone":"Warm","north":"Back"}""";
        var plain = RoomFinish.Default;
        var finish = new RoomFinish(plain.Walls, [], plain.CeilingColor, plain.TrimColor, plain.TimeOfDay, fittings: Fancy);

        var read = (RoomFinish)RoomFinishJson.Converter.ConvertFromProvider(older)!;
        var json = (string)RoomFinishJson.Converter.ConvertToProvider(finish)!;
        var back = (RoomFinish)RoomFinishJson.Converter.ConvertFromProvider(json)!;

        Assert.Equal(RoomFittings.Default, read.Fittings);
        Assert.Equal(finish, back);
        Assert.Equal(Fancy, back.Fittings);
    }
}
