using DesignApp.Application.Helpers;
using DesignApp.Application.UseCases;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Extensions;
using DesignApp.Domain.Objects;
using DesignApp.Infrastructure.Ai;
using DesignApp.Infrastructure.Ai.Claude;
using DesignApp.Infrastructure.Ai.Fakes;
using DesignApp.Infrastructure.Builder;
using DesignApp.Infrastructure.Photos;
using DesignApp.Tests;
using Microsoft.Extensions.Options;

using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests.Builder;

/// <summary>
/// Building a plan from a room photo: the cleanup pass that tidies the objects as the AI placed them, the use case with
/// the fake AI, and reading Claude's answer.
/// </summary>
public class PhotoPlanTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);
    private static readonly HexColor Grey = HexColor.Parse("#8A8580");
    private readonly FakeRooms _rooms = new();
    private readonly FakePlans _plans = new();
    private readonly FakeDesigns _designs = new();
    private readonly FakeStorage _storage = new();
    private readonly EmbeddedObjectCatalog _catalog = new();

    /// <summary>The use case with the fake AI, which answers every photo with the same rough living room (or another AI).</summary>
    /// <param name="user">The user asking.</param>
    /// <param name="planner">What reads the photo; the fake when missing.</param>
    private BuildPlanFromRoom Build(string user = "user-1", Application.Interfaces.IPhotoPlanner? planner = null)
    {
        var fake = planner ?? new FakePhotoPlanner(Options.Create(new AiOptions { UseFakes = true }));
        var ai = new FixedAiProviders(new FakeInventoryProvider(Options.Create(new AiOptions { UseFakes = true })), new FakeSegmentationProvider(Options.Create(new AiOptions())),
            photoPlanner: fake);

        return new BuildPlanFromRoom(_rooms, _plans, _storage, new SkiaRoomImaging(), ai, _catalog, new FixedUser(user), new FixedClock(Now), new PlanDesigns(_designs, _plans, new FixedClock(Now)));
    }

    /// <summary>An uploaded room with its photo stored.</summary>
    private async Task<Room> UploadedAsync()
    {
        var id = Guid.CreateVersion7(Now);
        var room = Room.Upload(id, "user-1", Application.Constants.Rooms.PhotoKey(id), 600, 400, Now, AnalysisScope.WholeRoom);

        _storage.Files[room.PhotoKey] = (TestImages.Create(600, 400), Application.Constants.ContentTypes.Jpeg);
        _rooms.Add(room);
        await _rooms.SaveChangesAsync(CancellationToken.None);

        return room;
    }

    /// <summary>Whether two objects on the floor overlap (rugs aside).</summary>
    /// <param name="a">One.</param>
    /// <param name="b">The other.</param>
    private static bool Overlap(PlanItem a, PlanItem b)
    {
        static (double X1, double Y1, double X2, double Y2) Box(PlanItem i)
        {
            var (w, d) = i.Rotation % 180 == 0 ? (i.Width, i.Depth) : (i.Depth, i.Width);

            return (i.X - w / 2, i.Y - d / 2, i.X + w / 2, i.Y + d / 2);
        }

        var (p, q) = (Box(a), Box(b));

        return p.X1 < q.X2 - 1 && q.X1 < p.X2 - 1 && p.Y1 < q.Y2 - 1 && q.Y1 < p.Y2 - 1;
    }

    [Fact]
    public void The_cleanup_moves_overlapping_objects_apart_and_pushes_wall_side_furniture_against_its_wall()
    {
        var sofa = new PlanItem("sofa", ItemShape.Sofa, 200, 300, 200, 90, 85, 180, Grey);
        var armchair = new PlanItem("armchair", ItemShape.Sofa, 250, 300, 85, 85, 85, 180, Grey);
        var sideboard = new PlanItem("sideboard", ItemShape.Box, 200, 50, 160, 40, 80, 0, Grey);
        var table = new PlanItem("coffee table", ItemShape.Table, 200, 200, 110, 60, 42, 0, Grey);

        var (items, leftOut) = PhotoLayout.Tidy(4, 5, 2.6, [sofa, armchair, sideboard, table]);
        var standing = items.Where(i => !i.Shape.IsOnWall()).ToList();

        Assert.Empty(leftOut);
        Assert.Equal(4, standing.Count);
        Assert.All(standing, a => Assert.DoesNotContain(standing, b => !ReferenceEquals(a, b) && Overlap(a, b)));
        // The sideboard stood 30 cm off the back wall: now against it. The table, not wall-side furniture, stays put.
        Assert.Equal(20, standing.Single(i => i.Label == "sideboard").Y);
        Assert.Equal((200.0, 200.0), (standing.Single(i => i.Label == "coffee table").X, standing.Single(i => i.Label == "coffee table").Y));
    }

    [Fact]
    public void The_cleanup_turns_a_bed_facing_its_headboard_wall_round_and_leaves_one_facing_the_room()
    {
        var backwards = new PlanItem("backwards bed", ItemShape.Bed, 120, 110, 160, 200, 50, 180, Grey);
        var right = new PlanItem("bed", ItemShape.Bed, 320, 300, 140, 190, 50, 180, Grey);

        var (items, _) = PhotoLayout.Tidy(4.5, 5, 2.6, [backwards, right]);
        var turned = items.Single(i => i.Label == "backwards bed");
        var kept = items.Single(i => i.Label == "bed");

        // Its foot was 10 cm from the back wall and its headboard faced the room: now the headboard is on that wall.
        Assert.Equal((0, 100.0), (turned.Rotation, turned.Y));
        Assert.Equal((180, 300.0), (kept.Rotation, kept.Y));
    }

    [Fact]
    public void The_cleanup_slides_a_hung_thing_off_a_window_and_leaves_out_what_fits_nowhere()
    {
        var window = new PlanItem("window", ItemShape.Window, 200, 0, 120, 10, 140, 0, Grey, (int)PlanWall.Back, 90);
        var painting = new PlanItem("painting", ItemShape.Picture, 210, 0, 80, 3, 60, 0, Grey, (int)PlanWall.Back, 130);
        var huge = new PlanItem("huge sofa", ItemShape.Sofa, 200, 250, 600, 95, 85, 0, Grey);

        var (items, leftOut) = PhotoLayout.Tidy(4, 5, 2.6, [window, painting, huge]);
        var hung = items.Single(i => i.Label == "painting");

        // Clear of the window's 140-260 cm along the wall.
        Assert.True(hung.X + hung.Width / 2 <= 140 || hung.X - hung.Width / 2 >= 260, $"painting at {hung.X}");
        Assert.Equal(["huge sofa"], leftOut);
    }

    [Fact]
    public void A_window_too_tall_or_past_its_walls_end_is_lowered_and_slid_and_a_hung_thing_on_the_floor_goes_on_the_nearest_wall()
    {
        var offered = PhotoPlanReader.Offered(new EmbeddedObjectCatalog());
        Application.Objects.PhotoPlanItem[] objects =
        [
            // Its top 290 cm up, under a 270 cm ceiling; and 10 cm past the back wall's right end.
            new("arched-window", "tall", 0, 0, 0, 120, 10, 230, "#F4F1EA", "", PlanWall.Back, 125, 60),
            new("wide-window", "standard", 0, 0, 0, 200, 10, 150, "#F4F1EA", "", PlanWall.Back, 360, 90),
            // A mirror answered as standing 20 cm from the right wall, 150 cm down it.
            new("mirror", "round", 380, 150, 0, 70, 3, 70, "#CFE3EE", "", null, 0, 130),
        ];
        var answer = new Application.Objects.PhotoPlan(RoomType.Bathroom, 4, 5, 2.7, "#E8E2D6", WallFinish.Paint, "#B89B7A", FloorType.Tiles,
            CeilingLight.FlushLight, new OutlinePoint(200, 480), objects);

        var ready = PhotoPlanReader.Read(answer, offered);
        var arched = ready.Items.Single(i => i.Label == "tall arched window");
        var wide = ready.Items.Single(i => i.Label == "wide window");
        var mirror = ready.Items.Single(i => i.Shape == ItemShape.Picture);

        Assert.Empty(ready.LeftOut);
        Assert.Equal((125.0, 40.0), (arched.X, arched.Elevation));
        Assert.True(wide.X + wide.Width / 2 <= 400, $"wide window at {wide.X}");
        Assert.Equal(((int?)PlanWall.Right, 150.0), (mirror.Wall, mirror.X));
    }

    [Fact]
    public async Task A_room_photo_becomes_a_tidied_plan_linked_to_the_room()
    {
        var room = await UploadedAsync();

        var outcome = await Build().HandleAsync(room.Id, null, null, CancellationToken.None);
        var plan = outcome!.Value.Plan;
        var standing = plan.Items.Where(i => !i.Shape.IsOnWall() && i.Shape != ItemShape.Rug).ToList();
        var window = plan.Items.Single(i => i.Shape == ItemShape.Window);
        var tv = plan.Items.Single(i => i.Label == "wall TV");
        var saved = await _plans.ListByOwnerAsync("user-1", CancellationToken.None);

        Assert.Equal((room.Id, RoomType.LivingRoom, 4.0, 5.0), (plan.FromRoomId!.Value, plan.RoomType, plan.Width, plan.Length));
        Assert.Contains(saved, p => p.Id == plan.Id);
        Assert.Empty(outcome.Value.LeftOut);
        // The fake's armchair overlapped the sofa, its TV stand stood off the wall and its TV crossed the window.
        Assert.All(standing, a => Assert.DoesNotContain(standing, b => !ReferenceEquals(a, b) && Overlap(a, b)));
        Assert.Equal(20, plan.Items.Single(i => i.Label == "TV stand").Y);
        Assert.True(tv.X + tv.Width / 2 <= window.X - window.Width / 2 || tv.X - tv.Width / 2 >= window.X + window.Width / 2, $"TV at {tv.X}");
        Assert.Equal("velvet", plan.Items.Single(i => i.Label == "three-seat sofa").Material);
        // The photo is seen from where it was taken, and nothing stands there.
        Assert.Equal((ViewCorner.Spot, new OutlinePoint(200, 470)), (plan.Corner, plan.CameraSpot));
        Assert.DoesNotContain(standing, i => Math.Abs(i.X - 200) < i.Width / 2 + 40 && Math.Abs(i.Y - 470) < i.Depth / 2 + 40);
    }

    [Fact]
    public async Task A_room_size_answered_in_centimetres_is_read_as_metres()
    {
        var room = await UploadedAsync();
        var inCentimetres = new Application.Objects.PhotoPlan(RoomType.LivingRoom, 460, 520, 260, "#E8E2D6", WallFinish.Paint, "#B89B7A", FloorType.Wood,
            CeilingLight.Pendant, new OutlinePoint(230, 480), []);

        var outcome = await Build(planner: new FixedPhotoPlanner(inCentimetres)).HandleAsync(room.Id, null, null, CancellationToken.None);
        var plan = outcome!.Value.Plan;

        Assert.Equal((4.6, 5.2, 2.6), (plan.Width, plan.Length, plan.Height));
    }

    [Fact]
    public async Task Only_the_rooms_owner_can_build_a_plan_from_it()
    {
        var room = await UploadedAsync();

        var outcome = await Build("someone-else").HandleAsync(room.Id, null, null, CancellationToken.None);
        var saved = await _plans.ListByOwnerAsync("someone-else", CancellationToken.None);

        Assert.Null(outcome);
        Assert.Empty(saved);
    }

    [Fact]
    public void Claudes_answer_is_read_into_a_photo_plan_and_a_broken_one_fails()
    {
        const string json = """
            {"roomType":"Bedroom","widthMetres":4.2,"lengthMetres":4.6,"heightMetres":2.6,"wallColor":"#EEEEEE","walls":"Plaster","floorColor":"#B89B7A",
             "floorType":"Wood","ceilingLight":"FlushLight","cameraX":60,"cameraY":420,"objects":[
              {"key":"double-bed","variant":"king","x":250,"y":110,"rotation":0,"width":180,"depth":210,"height":100,"color":"#E8DCC8",
               "material":"oak","wall":"None","along":0,"elevation":0},
              {"key":"window","variant":"standard","x":0,"y":0,"rotation":0,"width":120,"depth":10,"height":140,"color":"#F4F1EA",
               "material":"uPVC","wall":"Right","along":200,"elevation":90}]}
            """;

        var read = PhotoPlanJson.Parse(json);
        var broken = PhotoPlanJson.Parse("{\"roomType\":");

        Assert.True(read.Succeeded);
        Assert.Equal((RoomType.Bedroom, new OutlinePoint(60, 420), WallFinish.Plaster), (read.Value.RoomType, read.Value.Camera, read.Value.Walls));
        Assert.Equal((4.2, 4.6, 2.6), (read.Value.Width, read.Value.Length, read.Value.Height));
        Assert.Null(read.Value.Objects[0].Wall);
        Assert.Equal(PlanWall.Right, read.Value.Objects[1].Wall);
        Assert.False(broken.Succeeded);
    }
}
