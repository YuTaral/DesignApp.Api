using DesignApp.Application.Helpers;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;
using DesignApp.Infrastructure.Database;

using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests.Builder;

/// <summary>A window's curtains or blinds: only on windows, worded in the photo prompts, and stored with the plan.</summary>
public class CoveringTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);
    private static readonly HexColor Wall = HexColor.Parse("#E8E2D6");
    private static readonly HexColor Sage = HexColor.Parse("#8A9A84");
    private static readonly WindowCovering Velvet = new(CoveringKind.Curtains, Sage, " velvet ", CurtainLength.Floor);

    /// <summary>A window on the back wall, dressed as given.</summary>
    /// <param name="covering">Its covering; <c>null</c> for none.</param>
    private static PlanItem Window(WindowCovering? covering)
    {
        return new PlanItem("window", ItemShape.Window, 200, 0, 120, 10, 140, 0, Wall, (int)PlanWall.Back, 90, covering: covering);
    }

    [Fact]
    public void Only_a_window_has_a_covering_of_the_kinds_offered()
    {
        var window = Window(Velvet);

        Assert.Equal((CoveringKind.Curtains, "velvet", CurtainLength.Floor), (window.Covering!.Kind, window.Covering.Fabric, window.Covering.Length));
        Assert.True(window.Covering.IsCurtains);
        Assert.False(new WindowCovering(CoveringKind.RomanBlind, Sage, null, CurtainLength.Sill).IsCurtains);
        Assert.Throws<DomainException>(() => new PlanItem("sofa", ItemShape.Sofa, 200, 200, 200, 90, 85, 0, Wall, covering: Velvet));
        Assert.Throws<DomainException>(() => new WindowCovering((CoveringKind)99, Sage, null, CurtainLength.Sill));
        Assert.Throws<DomainException>(() => new WindowCovering(CoveringKind.Sheers, Sage, new string('x', Plans.MaxMaterialLength + 1), CurtainLength.Sill));
    }

    [Fact]
    public void The_photo_and_360_prompts_say_what_each_window_is_dressed_with()
    {
        var plan = RoomPlan.Create(Guid.CreateVersion7(Now), "user-1", "", RoomType.LivingRoom, 4, 5, 2.6, Wall, Wall, FloorType.Wood, AiMode.Fake, Now);
        var blind = new PlanItem("window", ItemShape.Window, 100, 0, 80, 10, 120, 0, Wall, (int)PlanWall.Back, 90,
            covering: new WindowCovering(CoveringKind.RollerBlind, Wall, "", CurtainLength.Sill));

        plan.Update("", 4, 5, 2.6, Wall, Wall, FloorType.Wood, PlanWall.Back, [Window(Velvet), blind], Now);
        var photo = PlanPrompt.Write(plan);
        var panorama = PanoramaPrompt.Write(plan);

        Assert.Contains(", dressed with floor-length velvet curtains in", photo, StringComparison.Ordinal);
        Assert.Contains("drawn open to both sides", photo, StringComparison.Ordinal);
        Assert.Contains(", with a roller blind in", photo, StringComparison.Ordinal);
        Assert.Contains(", dressed with floor-length velvet curtains in", panorama, StringComparison.Ordinal);
    }

    [Fact]
    public void A_covering_is_stored_with_the_plan_and_older_windows_have_none()
    {
        const string older = """[{"label":"window","shape":"Window","x":200,"y":0,"width":120,"depth":10,"height":140,"rotation":0,"color":"#E8E2D6","wall":0,"elevation":90}]""";
        IReadOnlyList<PlanItem> items = [Window(Velvet), Window(null)];

        var json = (string)PlanItemsJson.Converter.ConvertToProvider(items)!;
        var back = (IReadOnlyList<PlanItem>)PlanItemsJson.Converter.ConvertFromProvider(json)!;
        var read = (IReadOnlyList<PlanItem>)PlanItemsJson.Converter.ConvertFromProvider(older)!;

        Assert.Equal(Velvet, back[0].Covering);
        Assert.Null(back[1].Covering);
        Assert.Null(read[0].Covering);
    }
}
