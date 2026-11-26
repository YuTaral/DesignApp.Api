using DesignApp.Application.Helpers;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Extensions;
using DesignApp.Domain.Objects;
using DesignApp.Infrastructure.Builder;
using DesignApp.Infrastructure.Database;

using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests.Builder;

/// <summary>
/// A built room's lighting: ceiling lights as objects, lamps switched on or off (or following the time of day), the
/// color of the light, and which way the windows face, in a new room's layout and in the photo prompt.
/// </summary>
public class LightingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);
    private static readonly HexColor White = HexColor.Parse("#F4F1EA");
    private readonly EmbeddedObjectCatalog _catalog = new();

    /// <summary>A pendant light in a 4 × 5 m room's middle.</summary>
    /// <param name="lit">Whether it is switched on; <c>null</c> to follow the time of day.</param>
    private static PlanItem Pendant(bool? lit = null)
    {
        return new PlanItem("dome pendant light", ItemShape.PendantLight, 200, 250, 40, 40, 70, 0, White, material: "linen shade", lit: lit);
    }

    /// <summary>A 4 × 5 m living room looking at its back wall, with a window on it, a floor lamp and the given objects.</summary>
    /// <param name="finish">The room's finish.</param>
    /// <param name="items">More objects.</param>
    private static RoomPlan Room(RoomFinish finish, params PlanItem[] items)
    {
        var plan = RoomPlan.Create(Guid.CreateVersion7(Now), "user-1", "", RoomType.LivingRoom, 4, 5, 2.6, White, White, FloorType.Wood, AiMode.Fake, Now);
        var window = new PlanItem("window", ItemShape.Window, 200, 0, 120, 10, 140, 0, White, (int)PlanWall.Back, 90);
        var lamp = new PlanItem("floor lamp", ItemShape.Lamp, 60, 120, 40, 40, 160, 0, White);

        plan.Update("", plan.Outline, 2.6, White, White, FloorType.Wood, (int)PlanWall.Back, [window, lamp, .. items], Now, finish);

        return plan;
    }

    /// <summary>The default finish at a time of day, with its light's tone and north.</summary>
    /// <param name="time">When the photo is taken.</param>
    /// <param name="tone">The light's color.</param>
    /// <param name="north">The side of the plan facing north.</param>
    private static RoomFinish At(TimeOfDay time, LightTone tone = LightTone.Warm, PlanWall north = PlanWall.Back)
    {
        var plain = RoomFinish.Default;

        return new RoomFinish(plain.Walls, [], plain.CeilingColor, plain.TrimColor, time, tone, north);
    }

    [Fact]
    public void A_ceiling_light_hangs_from_the_ceiling_and_says_how_many_spots_it_has()
    {
        var row = new PlanItem("row of recessed spotlights", ItemShape.Spotlight, 200, 250, 240, 10, 3, 0, White);
        var grid = new PlanItem("grid of recessed spotlights", ItemShape.Spotlight, 200, 250, 260, 360, 3, 0, White);
        var track = new PlanItem("track light", ItemShape.TrackLight, 200, 250, 200, 6, 15, 90, White);

        Assert.Equal((3, 1), row.Spots());
        Assert.Equal((3, 4), grid.Spots());
        Assert.Equal(4, track.TrackHeads());
        Assert.Throws<DomainException>(() => new PlanItem("pendant", ItemShape.PendantLight, 200, 0, 40, 40, 70, 0, White, (int)PlanWall.Back));
        Assert.Throws<DomainException>(() => new PlanItem("pendant", ItemShape.PendantLight, 200, 250, 40, 40, 70, 0, White, elevation: 200));
        Assert.Throws<DomainException>(() => new PlanItem("pendant", ItemShape.PendantLight, 200, 250, 40, 40, 70, 0, White, decorations: new ItemDecorations(["fairy lights"])));
    }

    [Fact]
    public void A_light_is_on_as_switched_or_else_in_the_evening()
    {
        var lamp = new PlanItem("floor lamp", ItemShape.Lamp, 60, 120, 40, 40, 160, 0, White);
        var switchedOn = new PlanItem("floor lamp", ItemShape.Lamp, 60, 120, 40, 40, 160, 0, White, lit: true);
        var switchedOff = new PlanItem("floor lamp", ItemShape.Lamp, 60, 120, 40, 40, 160, 0, White, lit: false);
        var sconce = new PlanItem("wall sconce", ItemShape.WallBox, 100, 0, 15, 15, 25, 0, White, (int)PlanWall.Back, 160);
        var sideboard = new PlanItem("sideboard", ItemShape.Box, 200, 30, 160, 45, 80, 0, White, decorations: new ItemDecorations(["table lamp", "books"]));
        var sofa = new PlanItem("sofa", ItemShape.Sofa, 200, 200, 220, 95, 85, 0, White, lit: true);

        Assert.Equal((false, true), (lamp.IsOn(TimeOfDay.Midday), lamp.IsOn(TimeOfDay.Evening)));
        Assert.Equal((true, false), (switchedOn.IsOn(TimeOfDay.Midday), switchedOff.IsOn(TimeOfDay.Evening)));
        Assert.True(sconce.GivesLight() && sideboard.GivesLight() && Pendant().GivesLight());
        // Something that gives no light is never on, whatever it says.
        Assert.False(sofa.IsOn(TimeOfDay.Evening));
    }

    [Fact]
    public void A_new_room_starts_with_ceiling_lights_over_what_they_light()
    {
        var kitchen = StarterLayout.For(RoomType.Kitchen, 4, 5, 2.6, _catalog.ForRoom(RoomType.Kitchen));
        var dining = StarterLayout.For(RoomType.DiningRoom, 4, 5, 2.6, _catalog.ForRoom(RoomType.DiningRoom));
        var bedroom = StarterLayout.For(RoomType.Bedroom, 4, 5, 2.6, _catalog.ForRoom(RoomType.Bedroom));
        var island = kitchen.Single(i => i.Label.Contains("island", StringComparison.Ordinal));
        var table = dining.Single(i => i.Shape is ItemShape.Table);
        var chandelier = dining.Single(i => i.Shape is ItemShape.Chandelier);
        var lamp = bedroom.Single(i => i.Shape.IsCeilingLight());

        // A kitchen's spotlights spread over the room, 70 cm in from its walls, and two pendants hang over its island.
        Assert.Equal((260.0, 360.0), kitchen.Where(i => i.Shape is ItemShape.Spotlight).Select(i => (i.Width, i.Depth)).Single());
        Assert.Equal(2, kitchen.Count(i => i.Shape is ItemShape.PendantLight && i.Y == island.Y));
        Assert.Equal((table.X, table.Y), (chandelier.X, chandelier.Y));
        Assert.Equal((ItemShape.CeilingLamp, 200.0, 250.0), (lamp.Shape, lamp.X, lamp.Y));
        // None reaches lower than 180 cm above the floor.
        Assert.All(kitchen.Concat(dining).Where(i => i.Shape.IsCeilingLight()), i => Assert.True(260 - i.Height >= 180, i.Label));
    }

    [Fact]
    public void The_photo_prompt_lists_the_ceiling_lights_and_says_which_lights_are_on_and_their_color()
    {
        var spots = new PlanItem("row of recessed spotlights", ItemShape.Spotlight, 200, 150, 240, 10, 3, 0, White);
        var evening = PlanPrompt.Write(Room(At(TimeOfDay.Evening, LightTone.Neutral), Pendant(), spots));
        var midday = PlanPrompt.Write(Room(At(TimeOfDay.Midday), Pendant(lit: true)));
        var dark = PlanPrompt.Write(Room(At(TimeOfDay.Midday), Pendant()));

        Assert.Contains("On the ceiling: ", evening, StringComparison.Ordinal);
        Assert.Contains("linen shade dome pendant light: middle", evening, StringComparison.Ordinal);
        Assert.Contains("reaching 70 cm down from the ceiling", evening, StringComparison.Ordinal);
        Assert.Contains("switched on and glowing", evening, StringComparison.Ordinal);
        Assert.Contains("row of recessed spotlights (3 in a row)", evening, StringComparison.Ordinal);
        Assert.Contains("floor lamp: middle", evening, StringComparison.Ordinal);
        Assert.Contains("The lights that are switched on give neutral white (about 4000 K) light.", evening, StringComparison.Ordinal);
        Assert.Contains("lit only by its lamps and lights that are switched on", evening, StringComparison.Ordinal);
        // By day a lamp is off unless switched on; the pendant switched on is said so, with its light's color.
        Assert.Contains("floor lamp: middle 60 cm from the left wall", midday, StringComparison.Ordinal);
        Assert.Contains(", switched off", midday, StringComparison.Ordinal);
        Assert.Contains("give warm white (about 2700 K) light", midday, StringComparison.Ordinal);
        Assert.DoesNotContain("give warm white", dark, StringComparison.Ordinal);
    }

    [Fact]
    public void The_photo_prompt_says_which_way_the_windows_face_and_whether_the_sun_comes_in()
    {
        // The window is on the back wall: with north at the back it faces north, away from the midday sun.
        var north = PlanPrompt.Write(Room(At(TimeOfDay.Midday)));
        // With north at the front it faces south, and the midday sun shines in.
        var south = PlanPrompt.Write(Room(At(TimeOfDay.Midday, north: PlanWall.Front)));
        // With north on the left the back wall faces east: the morning sun shines in, the golden-hour sun doesn't.
        var morning = PlanPrompt.Write(Room(At(TimeOfDay.Morning, north: PlanWall.Left)));
        var golden = PlanPrompt.Write(Room(At(TimeOfDay.GoldenHour, north: PlanWall.Left)));
        var evening = PlanPrompt.Write(Room(At(TimeOfDay.Evening)));
        var panorama = PanoramaPrompt.Write(Room(At(TimeOfDay.Midday, north: PlanWall.Front), Pendant(lit: true)));

        Assert.Contains("The windows face north. The sun is round the other side of the house", north, StringComparison.Ordinal);
        Assert.Contains("The windows face south. The high midday sun shines straight in through the south-facing windows", south, StringComparison.Ordinal);
        Assert.Contains("The low morning sun shines straight in through the east-facing windows", morning, StringComparison.Ordinal);
        Assert.Contains("The windows face east. The sun is round the other side", golden, StringComparison.Ordinal);
        Assert.DoesNotContain("The windows face", evening, StringComparison.Ordinal);
        Assert.Contains("south-facing windows", panorama, StringComparison.Ordinal);
        Assert.Contains("dome pendant light", panorama, StringComparison.Ordinal);
        Assert.Contains("switched on and glowing", panorama, StringComparison.Ordinal);
    }

    [Fact]
    public void A_light_and_its_switch_are_stored_with_the_plan()
    {
        IReadOnlyList<PlanItem> items = [Pendant(lit: false), Pendant()];

        var json = (string)PlanItemsJson.Converter.ConvertToProvider(items)!;
        var back = (IReadOnlyList<PlanItem>)PlanItemsJson.Converter.ConvertFromProvider(json)!;

        Assert.Equal(items, back);
        Assert.Equal((false, (bool?)null), (back[0].Lit, back[1].Lit));
    }
}
