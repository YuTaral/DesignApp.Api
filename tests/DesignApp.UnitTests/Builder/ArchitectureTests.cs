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
/// A built room's architecture: its ceiling's shape, the mouldings, wainscoting and casings in its trim color, and the
/// archways, niches and columns placed like objects; how they are stored and worded in the photo prompt.
/// </summary>
public class ArchitectureTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);
    private static readonly HexColor White = HexColor.Parse("#F4F1EA");
    private static readonly HexColor Oak = HexColor.Parse(Plans.BeamColor);
    private readonly EmbeddedObjectCatalog _catalog = new();

    /// <summary>A 4 × 5 m living room looking at its back wall, with a window on it, in the default finish with the given details.</summary>
    /// <param name="details">The room's architecture.</param>
    /// <param name="items">More objects.</param>
    private static RoomPlan Room(RoomDetails details, params PlanItem[] items)
    {
        var plan = RoomPlan.Create(Guid.CreateVersion7(Now), "user-1", "", RoomType.LivingRoom, 4, 5, 2.6, White, White, FloorType.Wood, AiMode.Fake, Now);
        var window = new PlanItem("window", ItemShape.Window, 200, 0, 120, 10, 140, 0, White, (int)PlanWall.Back, 90);
        var plain = RoomFinish.Default;
        var finish = new RoomFinish(plain.Walls, [], plain.CeilingColor, plain.TrimColor, plain.TimeOfDay, details: details);

        plan.Update("", plan.Outline, 2.6, White, White, FloorType.Wood, (int)PlanWall.Back, [window, .. items], Now, finish);

        return plan;
    }

    [Fact]
    public void Details_keep_each_moulding_once_in_order_and_refuse_unknown_choices()
    {
        var details = new RoomDetails(CeilingStyle.Beams, Oak, [Moulding.DadoRail, Moulding.Crown], Wainscot.ShakerPanels, Casing.Wide);

        Assert.Equal([Moulding.Crown, Moulding.DadoRail], details.Mouldings);
        Assert.True(details.Has(Moulding.Crown));
        Assert.False(details.Has(Moulding.PictureRail));
        Assert.Equal(details, new RoomDetails(CeilingStyle.Beams, Oak, [Moulding.Crown, Moulding.DadoRail], Wainscot.ShakerPanels, Casing.Wide));
        Assert.Throws<DomainException>(() => new RoomDetails(CeilingStyle.Flat, Oak, [Moulding.Crown, Moulding.Crown], Wainscot.None, Casing.Simple));
        Assert.Throws<DomainException>(() => new RoomDetails((CeilingStyle)9, Oak, [], Wainscot.None, Casing.Simple));
        Assert.Throws<DomainException>(() => new RoomDetails(CeilingStyle.Flat, Oak, [(Moulding)9], Wainscot.None, Casing.Simple));
        Assert.Equal(RoomDetails.Default, RoomFinish.Default.Details);
    }

    [Fact]
    public void An_archway_is_an_opening_without_decorations_and_a_niche_hangs_on_a_wall_with_them()
    {
        var archway = new PlanItem("archway", ItemShape.Archway, 200, 0, 100, 5, 220, 0, White, (int)PlanWall.Back, form: ItemForm.Arched);
        var niche = new PlanItem("arched wall niche", ItemShape.Niche, 100, 0, 60, 20, 100, 0, White, (int)PlanWall.Back, 100, ItemForm.Arched,
            decorations: new ItemDecorations(["vases"]));

        Assert.True(ItemShape.Archway.IsOpening());
        Assert.True(ItemShape.Niche.IsMounted());
        Assert.Equal((0, Plans.MountedElevation), (ItemShape.Archway.UsualElevation(), ItemShape.Niche.UsualElevation()));
        Assert.Equal((ItemForm.Arched, 1), (archway.Form, niche.Decorations.Names.Count));
        Assert.Throws<DomainException>(() => new PlanItem("archway", ItemShape.Archway, 200, 0, 100, 5, 220, 0, White, (int)PlanWall.Back,
            decorations: new ItemDecorations(["wreath"])));
        Assert.Throws<DomainException>(() => new PlanItem("archway", ItemShape.Archway, 200, 0, 100, 5, 220, 0, White, (int)PlanWall.Back, 30));
    }

    [Fact]
    public void The_catalog_offers_archways_niches_and_columns_that_reach_the_ceiling()
    {
        var objects = _catalog.ForRoom(RoomType.LivingRoom);
        var archway = objects.Single(o => o.Key == "archway");
        var niche = objects.Single(o => o.Key == "niche");
        var columns = objects.Single(o => o.Key == "column").Variants ?? [];

        Assert.Equal((ItemShape.Archway, ItemForm.Arched), (archway.Shape, archway.Form));
        Assert.Equal(ItemShape.Niche, niche.Shape);
        Assert.NotEmpty(niche.Decorations!);
        Assert.All(columns, v => Assert.True(v.ToCeiling));
        Assert.Equal(ItemShape.Box, columns.Single(v => v.Key == "square").Shape);
        Assert.DoesNotContain(objects.SelectMany(o => o.Variants ?? []), v => v.ToCeiling && !columns.Contains(v));
    }

    [Fact]
    public void The_prompt_words_the_ceiling_the_mouldings_the_wainscoting_and_the_casings_in_the_trim_color()
    {
        var fancy = new RoomDetails(CeilingStyle.Beams, Oak, [Moulding.Crown, Moulding.DadoRail], Wainscot.ShakerPanels, Casing.Wide);
        var coffered = new RoomDetails(CeilingStyle.Coffered, Oak, [], Wainscot.None, Casing.Plain);
        var archway = new PlanItem("archway", ItemShape.Archway, 100, 0, 100, 5, 220, 0, White, (int)PlanWall.Back, form: ItemForm.Arched);

        var rich = PlanPrompt.Write(Room(fancy, archway));
        var plain = PlanPrompt.Write(Room(RoomDetails.Default));
        var bare = PlanPrompt.Write(Room(coffered));

        Assert.Contains("The ceiling has exposed wooden beams across it in", rich);
        Assert.Contains("The walls have crown moulding where they meet the ceiling, a dado rail at hip height, shaker-panel wainscoting up to 90 cm and wide flat casings around the doors and windows, all in", rich);
        Assert.Contains("archway", rich);
        // The default room says only its simple casings; a coffered one with plain reveals says only its ceiling.
        Assert.Contains("The walls have simple casings around the doors and windows, all in", plain);
        Assert.DoesNotContain("The ceiling has", plain);
        Assert.Contains("The ceiling is coffered", bare);
        Assert.DoesNotContain("The walls have", bare);
    }

    [Fact]
    public void Details_are_stored_and_a_finish_stored_before_them_reads_as_the_default()
    {
        const string older = """{"walls":"Paint","ownWalls":[],"ceilingColor":"#FAFAF7","trimColor":"#F4F1EA","timeOfDay":"Midday","tone":"Warm","north":"Back"}""";
        var details = new RoomDetails(CeilingStyle.Tray, HexColor.Parse("#5A4030"), [Moulding.PictureRail], Wainscot.Beadboard, Casing.Ornate);
        var plain = RoomFinish.Default;
        var finish = new RoomFinish(plain.Walls, [], plain.CeilingColor, plain.TrimColor, plain.TimeOfDay, details: details);

        var read = (RoomFinish)RoomFinishJson.Converter.ConvertFromProvider(older)!;
        var json = (string)RoomFinishJson.Converter.ConvertToProvider(finish)!;
        var back = (RoomFinish)RoomFinishJson.Converter.ConvertFromProvider(json)!;

        Assert.Equal(RoomDetails.Default, read.Details);
        Assert.Equal(finish, back);
        Assert.NotEqual(RoomFinish.Default, finish);
    }
}
