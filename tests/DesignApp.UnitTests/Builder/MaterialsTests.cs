using DesignApp.Application.Helpers;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;
using DesignApp.Infrastructure.Builder;
using DesignApp.Infrastructure.Database;

using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests.Builder;

/// <summary>
/// Materials in depth: an object's sheen, what a floor is made of within its type, and the painted walls' sheen; how
/// they are checked, stored and worded in the photo prompt, and the catalog's richer material lists.
/// </summary>
public class MaterialsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly HexColor White = HexColor.Parse("#F4F1EA");
    private static readonly HexColor Walnut = HexColor.Parse("#5C4033");
    private readonly EmbeddedObjectCatalog _catalog = new();

    /// <summary>A 4 × 5 m living room looking at its back wall, on a floor of a type, with a finish and objects.</summary>
    /// <param name="floor">The floor's type.</param>
    /// <param name="finish">The room's finish.</param>
    /// <param name="items">Its objects.</param>
    private static RoomPlan Room(FloorType floor, RoomFinish finish, params PlanItem[] items)
    {
        var plan = RoomPlan.Create(Guid.CreateVersion7(Now), "user-1", "", RoomType.LivingRoom, 4, 5, 2.6, White, White, floor, AiMode.Fake, Now);

        plan.Update("", plan.Outline, 2.6, White, White, floor, (int)PlanWall.Back, items, Now, finish);

        return plan;
    }

    /// <summary>The default finish with a floor material and a paint sheen.</summary>
    /// <param name="floorMaterial">What the floor is made of.</param>
    /// <param name="sheen">The paint's sheen.</param>
    private static RoomFinish Finished(string floorMaterial, PaintSheen sheen = PaintSheen.Matte)
    {
        var plain = RoomFinish.Default;

        return new RoomFinish(plain.Walls, [], plain.CeilingColor, plain.TrimColor, plain.TimeOfDay, floorMaterial: floorMaterial, wallSheen: sheen);
    }

    /// <summary>A sideboard against the back wall, in walnut with a sheen.</summary>
    /// <param name="sheen">Its sheen.</param>
    private static PlanItem Sideboard(Sheen? sheen)
    {
        return new PlanItem("sideboard", ItemShape.Box, 200, 30, 160, 45, 80, 0, Walnut, material: "walnut", sheen: sheen);
    }

    [Fact]
    public void A_floor_takes_only_its_types_materials_and_loses_its_material_when_its_type_changes()
    {
        var stone = Room(FloorType.Stone, Finished("Calacatta marble"));

        Assert.Equal("Calacatta marble", stone.Finish.FloorMaterial);
        Assert.Throws<DomainException>(() => Room(FloorType.Wood, Finished("Calacatta marble")));
        Assert.Throws<DomainException>(() => new PlanItem("sideboard", ItemShape.Box, 200, 30, 160, 45, 80, 0, Walnut, sheen: (Sheen)9));
        Assert.Throws<DomainException>(() => new RoomFinish(WallFinish.Paint, [], White, White, TimeOfDay.Midday, wallSheen: (PaintSheen)9));

        // Saved without a finish on a wooden floor, the marble no longer fits: the floor is any wood again.
        stone.Update("", stone.Outline, 2.6, White, White, FloorType.Wood, (int)PlanWall.Back, [], Now);

        Assert.Equal("", stone.Finish.FloorMaterial);
        Assert.Equal(Room(FloorType.Wood, Finished("")).Finish.FloorMaterial, stone.Finish.FloorMaterial);
    }

    [Fact]
    public void The_prompt_words_the_sheen_the_floors_material_and_the_paints_sheen_only_when_given()
    {
        var rich = PlanPrompt.Write(Room(FloorType.Stone, Finished("Calacatta marble", PaintSheen.Eggshell), Sideboard(Sheen.Gloss)));
        var plain = PlanPrompt.Write(Room(FloorType.Wood, Finished(""), Sideboard(null)));

        Assert.Contains("glossy walnut sideboard", rich);
        Assert.Contains("large Calacatta marble slabs", rich);
        Assert.Contains("The painted walls have a soft eggshell finish.", rich);
        Assert.Contains("walnut sideboard", plain);
        Assert.DoesNotContain("glossy", plain);
        Assert.DoesNotContain("painted walls have", plain);
        Assert.Contains("wooden plank flooring", plain);
    }

    [Fact]
    public void Sheens_and_the_floors_material_are_stored_and_older_rows_read_as_any_and_matte()
    {
        const string older = """{"walls":"Paint","ownWalls":[],"ceilingColor":"#FAFAF7","trimColor":"#F4F1EA","timeOfDay":"Midday","tone":"Warm","north":"Back"}""";
        var finish = Finished("walnut", PaintSheen.Satin);
        IReadOnlyList<PlanItem> items = [Sideboard(Sheen.Satin)];

        var read = (RoomFinish)RoomFinishJson.Converter.ConvertFromProvider(older)!;
        var back = (RoomFinish)RoomFinishJson.Converter.ConvertFromProvider((string)RoomFinishJson.Converter.ConvertToProvider(finish)!)!;
        var itemsBack = (IReadOnlyList<PlanItem>)PlanItemsJson.Converter.ConvertFromProvider((string)PlanItemsJson.Converter.ConvertToProvider(items)!)!;

        Assert.Equal(("", PaintSheen.Matte), (read.FloorMaterial, read.WallSheen));
        Assert.Equal(finish, back);
        Assert.Equal(Sheen.Satin, itemsBack[0].Sheen);
    }

    [Fact]
    public void The_catalog_offers_wood_species_stones_and_leathers()
    {
        var objects = _catalog.ForRoom(RoomType.LivingRoom);
        var all = objects.SelectMany(o => o.Materials ?? []).ToHashSet();

        Assert.Superset(new HashSet<string> { "walnut", "teak", "Calacatta marble", "travertine", "cognac leather", "brushed brass", "blackened steel" }, all);
        Assert.All(Plans.FloorMaterials, kind => Assert.NotEmpty(kind.Value));
    }
}
