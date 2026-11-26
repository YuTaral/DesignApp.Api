using DesignApp.Application.Helpers;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;
using DesignApp.Infrastructure.Builder;
using DesignApp.Infrastructure.Database;

using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests.Builder;

/// <summary>
/// A kitchen unit's worktop and backsplash: only on floor units, offered for the catalog's kitchen units, started in a
/// new kitchen, worded in the photo prompt, and stored with the plan.
/// </summary>
public class WorktopTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);
    private static readonly HexColor White = HexColor.Parse("#F4F1EA");
    private static readonly HexColor Green = HexColor.Parse("#4F6B3A");
    private static readonly Worktop Oak = new(" oak ", HexColor.Parse("#B8864B"), BacksplashKind.Tiles, Green);
    private readonly EmbeddedObjectCatalog _catalog = new();

    /// <summary>Base cabinets along the back wall with a worktop.</summary>
    /// <param name="worktop">The worktop; <c>null</c> for none.</param>
    private static PlanItem Cabinets(Worktop? worktop)
    {
        return new PlanItem("base cabinets", ItemShape.Box, 200, 30, 240, 60, 90, 0, White, worktop: worktop);
    }

    [Fact]
    public void Only_a_floor_unit_has_a_worktop_and_it_names_its_material()
    {
        Assert.Equal(("oak", BacksplashKind.Tiles), (Cabinets(Oak).Worktop!.Material, Cabinets(Oak).Worktop!.Backsplash));
        Assert.Throws<DomainException>(() => new PlanItem("sofa", ItemShape.Sofa, 200, 200, 200, 90, 85, 0, White, worktop: Oak));
        Assert.Throws<DomainException>(() => new Worktop(" ", White, BacksplashKind.None, White));
        Assert.Throws<DomainException>(() => new Worktop("quartz", White, (BacksplashKind)99, White));
    }

    [Fact]
    public void The_kitchen_units_offer_a_worktop_and_a_new_kitchen_starts_with_one()
    {
        var kitchen = _catalog.ForRoom(RoomType.Kitchen);
        var items = StarterLayout.For(RoomType.Kitchen, 4, 5, 2.6, kitchen);
        var topped = items.Where(i => i.Worktop is not null).ToList();

        Assert.Equal(WorktopSpot.Wall, kitchen.Single(o => o.Key == "base-cabinets").Worktop);
        Assert.Equal(WorktopSpot.Island, kitchen.Single(o => o.Key == "island").Worktop);
        Assert.Null(kitchen.Single(o => o.Key == "cooker").Worktop);
        Assert.NotEmpty(topped);
        Assert.All(topped, i => Assert.Equal("quartz", i.Worktop!.Material));
        Assert.Contains(topped, i => i.Worktop!.Backsplash is BacksplashKind.Upstand);
    }

    [Fact]
    public void The_photo_prompt_words_the_worktop_and_its_backsplash()
    {
        var plan = RoomPlan.Create(Guid.CreateVersion7(Now), "user-1", "", RoomType.Kitchen, 4, 5, 2.6, White, White, FloorType.Tiles, AiMode.Fake, Now);

        plan.Update("", 4, 5, 2.6, White, White, FloorType.Tiles, PlanWall.Back, [Cabinets(Oak)], Now);
        var prompt = PlanPrompt.Write(plan);

        Assert.Contains(", with an oak worktop in", prompt, StringComparison.Ordinal);
        Assert.Contains("and a backsplash of tiles in", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void A_worktop_is_stored_with_the_plan()
    {
        IReadOnlyList<PlanItem> items = [Cabinets(Oak), Cabinets(null)];

        var json = (string)PlanItemsJson.Converter.ConvertToProvider(items)!;
        var back = (IReadOnlyList<PlanItem>)PlanItemsJson.Converter.ConvertFromProvider(json)!;

        Assert.Equal(Oak, back[0].Worktop);
        Assert.Null(back[1].Worktop);
    }
}
