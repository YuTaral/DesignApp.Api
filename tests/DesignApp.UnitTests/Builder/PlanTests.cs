using DesignApp.Application.Helpers;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Application.UseCases;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Extensions;
using DesignApp.Domain.Objects;
using DesignApp.Infrastructure.Ai;
using DesignApp.Infrastructure.Ai.Fakes;
using DesignApp.Infrastructure.Builder;
using DesignApp.Infrastructure.Database;
using DesignApp.Infrastructure.Photos;
using DesignApp.Tests;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests.Builder;

/// <summary>
/// The room builder: the rules of a built room and its objects, the common objects of each room type, the fake AI, and
/// turning a built room into a photo and a new room.
/// </summary>
public class PlanTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 20, 0, 0, TimeSpan.Zero);
    private static readonly IOptions<AiOptions> NoDelay = Options.Create(new AiOptions { UseFakes = true });
    private static readonly HexColor Wall = HexColor.Parse("#E8E2D6");
    private static readonly HexColor Floor = HexColor.Parse("#B89B7A");

    private readonly FakePlans _plans = new();
    private readonly FakeRooms _rooms = new();
    private readonly FakeStorage _storage = new();
    private readonly RecordingScheduler _scheduler = new();
    private readonly FakeDesigns _designs = new();

    private static RoomPlan NewPlan(double width = 4, double length = 5, double height = 2.6)
    {
        return RoomPlan.Create(Guid.CreateVersion7(Now), "user-1", "", RoomType.LivingRoom, width, length, height, Wall, Floor, FloorType.Wood, AiMode.Fake, Now);
    }

    private static PlanItem Sofa(double x = 200, double y = 80)
    {
        return new PlanItem("sofa", ItemShape.Sofa, x, y, 220, 95, 85, 0, HexColor.Parse("#8A8580"));
    }

    [Theory]
    [InlineData(0.5, 5, 2.6)]
    [InlineData(4, 31, 2.6)]
    [InlineData(4, 5, 1.9)]
    [InlineData(4, 5, double.NaN)]
    public void A_built_room_keeps_its_walls_in_range(double width, double length, double height)
    {
        Assert.Equal(DesignApp.Domain.Constants.Messages.PlanWalls, Assert.Throws<DomainException>(() => NewPlan(width, length, height)).Message);
    }

    [Fact]
    public void A_built_room_has_a_known_type_a_short_name_and_at_most_sixty_objects()
    {
        Assert.Throws<DomainException>(() => RoomPlan.Create(Guid.NewGuid(), "user-1", "", (RoomType)99, 4, 5, 2.6, Wall, Floor, FloorType.Wood, AiMode.Fake, Now));
        Assert.Throws<DomainException>(() => RoomPlan.Create(Guid.NewGuid(), "user-1", new string('x', 81), RoomType.Bedroom, 4, 5, 2.6, Wall, Floor, FloorType.Wood, AiMode.Fake, Now));
        var plan = NewPlan();

        Assert.Equal(DesignApp.Domain.Constants.Messages.PlanItems,
            Assert.Throws<DomainException>(() => plan.Update("", 4, 5, 2.6, Wall, Floor, FloorType.Wood, PlanWall.Back, Enumerable.Repeat(Sofa(), 61).ToList(), Now)).Message);

        plan.Update("  Our living room ", 4.2, 5, 2.7, Wall, Floor, FloorType.Wood, PlanWall.Back, [Sofa()], Now.AddMinutes(1));
        Assert.Equal(("Our living room", 4.2, 1, Now.AddMinutes(1)), (plan.Name, plan.Width, plan.Items.Count, plan.UpdatedAt));
    }

    [Fact]
    public void An_object_needs_a_name_real_sides_a_position_and_a_quarter_turn()
    {
        var color = HexColor.Parse("#FFFFFF");

        Assert.Throws<DomainException>(() => new PlanItem(" ", ItemShape.Box, 0, 0, 10, 10, 10, 0, color));
        Assert.Throws<DomainException>(() => new PlanItem("box", ItemShape.Box, 0, 0, 0, 10, 10, 0, color));
        Assert.Throws<DomainException>(() => new PlanItem("box", ItemShape.Box, 0, 0, 10, 10, 3001, 0, color));
        Assert.Throws<DomainException>(() => new PlanItem("box", ItemShape.Box, double.NaN, 0, 10, 10, 10, 0, color));
        Assert.Throws<DomainException>(() => new PlanItem("box", ItemShape.Box, 0, 0, 10, 10, 10, 45, color));
        Assert.Throws<DomainException>(() => new PlanItem("box", (ItemShape)99, 0, 0, 10, 10, 10, 0, color));
        Assert.Equal(270, new PlanItem(" box ", ItemShape.Box, 0, 0, 10, 10, 10, 270, color).Rotation);
    }

    [Fact]
    public void Every_room_type_offers_windows_doors_and_at_least_twelve_common_objects_of_real_sizes()
    {
        var catalog = new EmbeddedObjectCatalog();

        Assert.All(Enum.GetValues<RoomType>(), type =>
        {
            var objects = catalog.ForRoom(type);

            Assert.True(objects.Count(o => !o.Shape.IsOnWall()) >= 12, $"{type}: {objects.Count(o => !o.Shape.IsOnWall())} objects on the floor");
            // Every room offers things to hang: a painting, a mirror and a wall shelf at least.
            Assert.True(objects.Count(o => o.Shape.IsMounted()) >= 3);
            Assert.All(objects.Where(o => o.Shape.IsMounted()), o => Assert.True(o.Elevation > 0 && o.Depth <= Plans.MaxMountedDepth));
            Assert.Contains(objects, o => o.Shape == ItemShape.Window);
            Assert.Contains(objects, o => o.Shape == ItemShape.Door);
            Assert.Equal(objects.Count, objects.Select(o => o.Key).Distinct().Count());
            // Each comes in three variations, the first being the object itself, and each could be placed as it is:
            // windows, doors and hung things on a wall, the rest on the floor.
            Assert.All(objects, o =>
            {
                Assert.Equal(3, o.Variants!.Count);
                Assert.Equal(3, o.Variants.Select(v => v.Key).Distinct().Count());
                Assert.Equal((o.Label, o.Shape, o.Width, o.Depth, o.Height, o.Form), (o.Variants[0].Label, o.Variants[0].Shape, o.Variants[0].Width, o.Variants[0].Depth, o.Variants[0].Height, o.Variants[0].Form));
                Assert.All(o.Variants, v => _ = new PlanItem(v.Label, v.Shape, 0, 0, v.Width, v.Depth, v.Height, 0, o.Color,
                    v.Shape.IsOnWall() ? (int)PlanWall.Back : null, v.Shape is ItemShape.Window || v.Shape.IsMounted() ? v.Elevation : 0, v.Form));
            });
        });
        var sofa = catalog.ForRoom(RoomType.LivingRoom).Single(o => o.Key == "sofa");

        Assert.Equal(ItemShape.Sofa, sofa.Shape);
        Assert.Contains(sofa.Variants!, v => v.Name == "L-shaped" && v.Form == ItemForm.LShaped);
        Assert.Contains(sofa.Variants!, v => v.Name == "P-shaped" && v.Form == ItemForm.PShaped);
        // Each room's names are unique, so an object on the plan tells which variation it is.
        Assert.All(Enum.GetValues<RoomType>(), type =>
        {
            // Up to five distinct decorations per object, each valid on a plan object.
            Assert.All(catalog.ForRoom(type), o => Assert.True(o.Decorations!.Count is 0 or 5 && new ItemDecorations(o.Decorations).Names.Count == o.Decorations.Count, o.Key));
            var labels = catalog.ForRoom(type).SelectMany(o => o.Variants!).Select(v => v.Label).ToList();

            Assert.Equal(labels.Count, labels.Distinct().Count());
        });
    }

    [Fact]
    public void Objects_are_stored_and_read_back()
    {
        IReadOnlyList<PlanItem> items = [Sofa(), new PlanItem("rug", ItemShape.Rug, 200, 250, 200, 300, 1, 90, HexColor.Parse("#D8C3A5"))];

        var json = (string)PlanItemsJson.Converter.ConvertToProvider(items)!;
        var back = (IReadOnlyList<PlanItem>)PlanItemsJson.Converter.ConvertFromProvider(json)!;

        Assert.Equal(items, back);
        Assert.Contains("\"shape\":\"Rug\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_fake_ai_reads_the_kind_size_and_color_from_the_words()
    {
        var table = (await new FakeRoomBuilderAi(NoDelay).DescribeObjectAsync(RoomType.LivingRoom, "A round oak coffee table, 90 cm", CancellationToken.None)).Value;

        Assert.Equal((ItemShape.Table, 90.0, "#B8864B"), (table.Shape, table.Width, table.Color.Value));
        Assert.Equal(ItemShape.Box, (await new FakeRoomBuilderAi(NoDelay).DescribeObjectAsync(RoomType.Kitchen, "something", CancellationToken.None)).Value.Shape);
    }

    [Fact]
    public void Windows_and_doors_go_on_a_wall_and_must_fit_on_it()
    {
        var white = HexColor.Parse("#F4F1EA");

        Assert.Throws<DomainException>(() => new PlanItem("window", ItemShape.Window, 100, 0, 120, 10, 140, 0, white));
        Assert.Throws<DomainException>(() => new PlanItem("sofa", ItemShape.Sofa, 100, 80, 220, 95, 85, 0, white, (int)PlanWall.Back));
        Assert.Throws<DomainException>(() => new PlanItem("door", ItemShape.Door, 100, 0, 90, 5, 210, 0, white, (int)PlanWall.Left, 20));
        var plan = NewPlan(width: 4, length: 3);

        // The left wall is the room's length (300 cm) long; the window's middle at 280 puts its end past it.
        var tooFar = new PlanItem("window", ItemShape.Window, 280, 0, 120, 10, 140, 0, white, (int)PlanWall.Left, 90);

        Assert.Throws<DomainException>(() => plan.Update("", 4, 3, 2.6, Wall, Floor, FloorType.Tiles, PlanWall.Left, [tooFar], Now));
        var tooHigh = new PlanItem("window", ItemShape.Window, 200, 0, 120, 10, 140, 0, white, (int)PlanWall.Back, 150);

        Assert.Throws<DomainException>(() => plan.Update("", 4, 3, 2.6, Wall, Floor, FloorType.Tiles, PlanWall.Left, [tooHigh], Now));
        Assert.Throws<DomainException>(() => plan.Update("", 4, 3, 2.6, Wall, Floor, (FloorType)99, PlanWall.Left, [], Now));

        var window = new PlanItem("window", ItemShape.Window, 200, 0, 120, 10, 140, 0, white, (int)PlanWall.Back, 90);

        plan.Update("", 4, 3, 2.6, Wall, Floor, FloorType.Tiles, PlanWall.Left, [window], Now);
        Assert.Equal((FloorType.Tiles, (int)PlanWall.Left, 300.0), (plan.FloorType, plan.View, plan.WallCentimetres((int)PlanWall.Left)));
    }

    [Fact]
    public void Windows_and_doors_are_stored_with_their_wall_and_height()
    {
        IReadOnlyList<PlanItem> items = [Sofa(), new PlanItem("window", ItemShape.Window, 150, 0, 120, 10, 140, 0, HexColor.Parse("#F4F1EA"), (int)PlanWall.Right, 90)];

        var json = (string)PlanItemsJson.Converter.ConvertToProvider(items)!;
        var back = (IReadOnlyList<PlanItem>)PlanItemsJson.Converter.ConvertFromProvider(json)!;

        Assert.Equal(items, back);
        Assert.Contains("\"wall\":1", json, StringComparison.Ordinal);
        // An object of an older plan, stored before walls existed, still reads.
        var old = (IReadOnlyList<PlanItem>)PlanItemsJson.Converter.ConvertFromProvider(
            """[{"label":"sofa","shape":"Sofa","x":200,"y":80,"width":220,"depth":95,"height":85,"rotation":0,"color":"#8A8580"}]""")!;

        Assert.Equal((null, 0.0), (old[0].Wall, old[0].Elevation));
    }

    [Fact]
    public void The_prompt_gives_the_exact_sizes_and_places_as_the_photo_sees_them()
    {
        var plan = NewPlan();
        var window = new PlanItem("window", ItemShape.Window, 100, 0, 120, 10, 140, 0, HexColor.Parse("#F4F1EA"), (int)PlanWall.Back, 90);
        var door = new PlanItem("door", ItemShape.Door, 150, 0, 90, 5, 210, 0, HexColor.Parse("#F4F1EA"), (int)PlanWall.Front);
        var sideWindow = new PlanItem("side window", ItemShape.Window, 400, 0, 120, 10, 140, 0, HexColor.Parse("#F4F1EA"), (int)PlanWall.Left, 90);
        var armchair = new PlanItem("armchair", ItemShape.Chair, 200, 480, 80, 80, 90, 0, HexColor.Parse("#D9A93A"));

        plan.Update("", 4, 5, 2.6, Wall, Floor, FloorType.Parquet, PlanWall.Back, [Sofa(210, 60), window, door, sideWindow, armchair], Now);

        var text = PlanPrompt.Write(plan);

        Assert.Contains("realistic photograph of the same living room interior", text, StringComparison.Ordinal);
        Assert.Contains("keep the rest of the room empty", text, StringComparison.Ordinal);
        Assert.Contains("400 cm wide from the left wall to the right wall, 500 cm deep from the camera to the far wall, and 260 cm from floor to ceiling", text, StringComparison.Ordinal);
        Assert.Contains("herringbone wooden parquet in tan (#B89B7A)", text, StringComparison.Ordinal);
        Assert.Contains("1. warm grey (#8A8580) sofa: middle 210 cm from the left wall and 60 cm from the far wall, 220 × 95 × 85 cm, facing the camera.", text, StringComparison.Ordinal);
        Assert.Contains("off-white (#F4F1EA) window, on the far wall, its middle 100 cm from the left wall, 120 cm wide and 140 cm high, its bottom 90 cm above the floor", text, StringComparison.Ordinal);
        Assert.Contains("off-white (#F4F1EA) side window, on the left wall, its middle 100 cm from the far wall", text, StringComparison.Ordinal);
        // The door is on the wall behind the camera and the armchair right in front of it, so the photo can't show them.
        Assert.DoesNotContain("(#F4F1EA) door", text, StringComparison.Ordinal);
        Assert.DoesNotContain("armchair", text, StringComparison.Ordinal);

        // Looking at the left wall: the plan's back wall is on the photo's right, and the sofa (facing the plan's front) faces left.
        // The first sofa is now beside the camera, only partly in view; the second is whole (the wide lens shows the side
        // walls from halfway to the far wall); the window on the right wall shows, the door beside the camera does not.
        plan.Update("", 4, 5, 2.6, Wall, Floor, FloorType.Parquet, PlanWall.Left, [Sofa(210, 60), Sofa(120, 250), window, door, sideWindow], Now);
        var sideways = PlanPrompt.Write(plan);

        Assert.Contains("500 cm wide from the left wall to the right wall, 400 cm deep", sideways, StringComparison.Ordinal);
        Assert.Contains("1. warm grey (#8A8580) sofa: middle 440 cm from the left wall and 210 cm from the far wall, 95 × 220 × 85 cm, facing left, partly cut off by the right and bottom edges of the photo;", sideways, StringComparison.Ordinal);
        Assert.Contains("2. warm grey (#8A8580) sofa: middle 250 cm from the left wall and 120 cm from the far wall, 95 × 220 × 85 cm, facing left.", sideways, StringComparison.Ordinal);
        Assert.Contains("Windows and doors, and no others: off-white (#F4F1EA) window, on the right wall, its middle 100 cm from the far wall, 120 cm wide and 140 cm high, its bottom 90 cm above the floor; off-white (#F4F1EA) side window, on the far wall, its middle 400 cm from the left wall", sideways, StringComparison.Ordinal);
        Assert.DoesNotContain("(#F4F1EA) door", sideways, StringComparison.Ordinal);
        Assert.Equal("living room", PlanPrompt.Words(nameof(RoomType.LivingRoom)));
    }

    [Fact]
    public async Task A_description_must_be_given_and_short()
    {
        var plan = NewPlan();

        _plans.Add(plan);
        var describe = new DescribePlanObject(_plans, new FixedUser("user-1"), Ai());

        Assert.Equal(DesignApp.Application.Constants.Messages.ObjectDescription, (await describe.HandleAsync(plan.Id, "  ", CancellationToken.None))!.Error);
        Assert.Equal(DesignApp.Application.Constants.Messages.ObjectDescription, (await describe.HandleAsync(plan.Id, new string('x', 301), CancellationToken.None))!.Error);
        Assert.True((await describe.HandleAsync(plan.Id, "a lamp", CancellationToken.None))!.Succeeded);
        Assert.Null(await new DescribePlanObject(_plans, new FixedUser("someone-else"), Ai()).HandleAsync(plan.Id, "a lamp", CancellationToken.None));
    }

    [Fact]
    public async Task A_room_started_from_a_design_puts_its_photos_there_and_only_in_the_users_own_design_with_room()
    {
        var clock = new FixedClock(Now);
        var create = new CreatePlan(_plans, new FixedUser("user-1"), clock, new EmbeddedObjectCatalog(), new PlanDesigns(_designs, _plans, clock));
        var design = Design.Create(UnitTests.Domain.RoomTests.ReadyRoom(), "user-1", "Flat", null, [], Now);
        var theirs = Design.Create(UnitTests.Domain.RoomTests.ReadyRoom("someone-else"), "someone-else", "Theirs", null, [], Now);
        var full = Design.Create(UnitTests.Domain.RoomTests.ReadyRoom(), "user-1", "Full", null, [], Now);

        for (var i = 1; i < Designs.MaxRooms; i++)
        {
            full.AddRoom(UnitTests.Domain.RoomTests.ReadyRoom(), Now);
        }

        _designs.Add(design);
        _designs.Add(theirs);
        _designs.Add(full);
        var made = await create.HandleAsync("", RoomType.LivingRoom, 4, 5, 2.6, Wall, Floor, FloorType.Wood, AiMode.Fake, true, null, design.Id, null, CancellationToken.None);

        Assert.True(made.Succeeded, made.Error);
        var photo = await Generator().HandleAsync(made.Value.Id, TestImages.Create(1280, 960), PhotoStyle.AsBuilt, null, null, null, null, new PhotoRun(null, null), CancellationToken.None);
        var refusedTheirs = await create.HandleAsync("", RoomType.Bedroom, 4, 4, 2.6, Wall, Floor, FloorType.Wood, AiMode.Fake, true, null, theirs.Id, null, CancellationToken.None);
        var refusedFull = await create.HandleAsync("", RoomType.Bedroom, 4, 4, 2.6, Wall, Floor, FloorType.Wood, AiMode.Fake, true, null, full.Id, null, CancellationToken.None);

        // The plan is the design's from the start, and its first photo's room goes there (not into a design of its own).
        Assert.Equal(design.Id, made.Value.DesignId);
        Assert.True(photo!.Succeeded, photo.Error);
        Assert.Equal(2, design.Rooms.Count);
        Assert.Contains(design.Rooms, r => r.RoomId == photo.Value.Id);
        Assert.Equal(DesignApp.Application.Constants.Messages.PlanDesignMissing, refusedTheirs.Error);
        Assert.Equal(DesignApp.Application.Constants.Messages.PlanDesignAlreadyFull("Full"), refusedFull.Error);
    }

    [Fact]
    public async Task Generating_makes_a_photo_and_a_new_room_with_the_plans_ai()
    {
        var plan = NewPlan();

        _plans.Add(plan);
        var generate = Generator();
        var guide = TestImages.Create(1280, 960);

        Assert.Equal(DesignApp.Application.Constants.Messages.EmptyPlan, (await generate.HandleAsync(plan.Id, guide, PhotoStyle.AsBuilt, null, null, null, null, new PhotoRun(null, null), CancellationToken.None))!.Error);
        plan.Update("", 4, 5, 2.6, Wall, Floor, FloorType.Wood, PlanWall.Back, [Sofa()], Now);
        Assert.Equal(DesignApp.Application.Constants.Messages.GuideImage, (await generate.HandleAsync(plan.Id, [], PhotoStyle.AsBuilt, null, null, null, null, new PhotoRun(null, null), CancellationToken.None))!.Error);
        Assert.Equal(DesignApp.Application.Constants.Messages.UnknownPhotoStyle, (await generate.HandleAsync(plan.Id, guide, (PhotoStyle)99, null, null, null, null, new PhotoRun(null, null), CancellationToken.None))!.Error);
        Assert.Equal(DesignApp.Application.Constants.Messages.PhotoInstructions,
            (await generate.HandleAsync(plan.Id, guide, PhotoStyle.Boho, new string('x', 501), null, null, null, new PhotoRun(null, null), CancellationToken.None))!.Error);

        var made = await generate.HandleAsync(plan.Id, guide, PhotoStyle.AsBuilt, null, null, null, null, new PhotoRun(null, null), CancellationToken.None);

        Assert.True(made!.Succeeded, made.Error);
        var room = made.Value;

        // The room waits until the user starts its analysis (with the real AI it costs money).
        Assert.Equal((RoomStatus.Waiting, AiMode.Fake, 1280), (room.Status, room.AiMode, room.Width));
        Assert.True(_storage.Files.ContainsKey(room.PhotoKey));
        Assert.Empty(_scheduler.Scheduled);
        Assert.Null(await Generator("someone-else").HandleAsync(plan.Id, guide, PhotoStyle.AsBuilt, null, null, null, null, new PhotoRun(null, null), CancellationToken.None));

        // One call, decorations or not: the 3D view and the prompt, which names each object's decorations with it.
        var images = new RecordingImageGenerator();
        var cushioned = new PlanItem("sofa", ItemShape.Sofa, 200, 80, 220, 95, 85, 0, HexColor.Parse("#8A8580"), decorations: new ItemDecorations(["cushions", "throw blanket"]));

        plan.Update("", 4, 5, 2.6, Wall, Floor, FloorType.Wood, PlanWall.Back, [cushioned], Now);
        Assert.True((await Generator(images: images).HandleAsync(plan.Id, guide, PhotoStyle.Boho, null, null, null, null, new PhotoRun(null, null), CancellationToken.None))!.Succeeded);
        var (image, prompt, seed, boxes) = Assert.Single(images.Calls);

        Assert.Null(boxes);
        Assert.Same(guide, image);
        Assert.Null(seed);
        // A seed makes the photo repeatable, for comparing changes fairly; it can't be negative.
        Assert.True((await Generator(images: images).HandleAsync(plan.Id, guide, PhotoStyle.Boho, null, 42, null, null, new PhotoRun(null, null), CancellationToken.None))!.Succeeded);
        Assert.Equal(42, images.Calls[^1].Seed);
        // Object boxes go to the image model with each object described; boxes off the plan or the picture are refused.
        Assert.True((await Generator(images: images).HandleAsync(plan.Id, guide, PhotoStyle.Boho, null, null, [new ItemBox(0, 0.1, 0.4, 0.6, 0.9)], null, new PhotoRun(null, null), CancellationToken.None))!.Succeeded);
        var box = Assert.Single(images.Calls[^1].Boxes!);

        Assert.Equal(("object1", "warm grey (#8A8580) sofa, decorated with cushions and throw blanket", 0.1, 0.9), (box.Label, box.Description, box.Left, box.Bottom));

        foreach (var bad in new[] { new ItemBox(1, 0.1, 0.4, 0.6, 0.9), new ItemBox(0, 0.6, 0.4, 0.1, 0.9), new ItemBox(0, -0.1, 0.4, 0.6, 1.2) })
        {
            Assert.Equal(DesignApp.Application.Constants.Messages.PhotoBoxes, (await generate.HandleAsync(plan.Id, guide, PhotoStyle.AsBuilt, null, null, [bad], null, new PhotoRun(null, null), CancellationToken.None))!.Error);
        }

        Assert.Equal(DesignApp.Application.Constants.Messages.PhotoSeed, (await generate.HandleAsync(plan.Id, guide, PhotoStyle.AsBuilt, null, -1, null, null, new PhotoRun(null, null), CancellationToken.None))!.Error);
        Assert.Contains("facing the camera, decorated with cushions and throw blanket", prompt, StringComparison.Ordinal);
        // A photo of another view (several can be made at once) is written from that view, and the plan keeps its own.
        Assert.True((await Generator(images: images).HandleAsync(plan.Id, guide, PhotoStyle.Boho, null, null, null, new PlanShot((int)PlanWall.Front, ViewCorner.Left), new PhotoRun(null, null), CancellationToken.None))!.Succeeded);
        Assert.Contains("near left corner", images.Calls[^1].Prompt, StringComparison.Ordinal);
        Assert.Equal((0, ViewCorner.None), (plan.View, plan.Corner));

        foreach (var bad in new[] { new PlanShot(4, ViewCorner.None), new PlanShot(-1, ViewCorner.None), new PlanShot(0, (ViewCorner)9) })
        {
            Assert.Equal(DesignApp.Application.Constants.Messages.PhotoShot, (await generate.HandleAsync(plan.Id, guide, PhotoStyle.AsBuilt, null, null, null, bad, new PhotoRun(null, null), CancellationToken.None))!.Error);
        }
    }

    [Fact]
    public async Task A_runs_later_photos_copy_the_objects_from_its_first_photo()
    {
        var plan = NewPlan();
        var images = new RecordingImageGenerator();
        var guide = TestImages.Create(1280, 960);
        var otherView = TestImages.Create(1024, 768);
        var here = new ItemBox(0, 0.1, 0.4, 0.6, 0.9);
        var there = new ItemBox(0, 0.5, 0.3, 0.9, 0.8);

        _plans.Add(plan);
        plan.Update("", 4, 5, 2.6, Wall, Floor, FloorType.Wood, PlanWall.Back, [Sofa()], Now);
        var first = (await Generator(images: images).HandleAsync(plan.Id, guide, PhotoStyle.AsBuilt, null, 7, [there], null, new PhotoRun(null, "back"), CancellationToken.None))!.Value;
        var second = await Generator(images: images).HandleAsync(plan.Id, otherView, PhotoStyle.AsBuilt, null, 7,
            [here], new PlanShot((int)PlanWall.Front, ViewCorner.None), new PhotoRun(first.Id, "front", null, [there]), CancellationToken.None);
        var copied = Assert.Single(images.Calls[1].Boxes!);

        // The first photo has no reference; the second takes the first as it was made, and copies the sofa from its box there.
        Assert.True(second!.Succeeded, second.Error);
        Assert.Null(images.References[0]);
        Assert.Equal(_storage.Files[Assert.Single(_rooms.Takes, t => t.Note == "back").Key].Content, images.References[1]);
        Assert.Equal(new SourceBox(0.5, 0.3, 0.9, 0.8), copied.FromReference);
        Assert.Equal((0.1, 0.9), (copied.Left, copied.Bottom));
        Assert.Contains("The second image is a photograph of this same room", images.Calls[1].Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("second image", images.Calls[0].Prompt, StringComparison.Ordinal);
        Assert.Equal(7, images.Calls[1].Seed);
        // Reference boxes off the plan are refused like the others.
        Assert.Equal(DesignApp.Application.Constants.Messages.PhotoBoxes, (await Generator(images: images).HandleAsync(plan.Id, otherView, PhotoStyle.AsBuilt, null, null,
            [here], null, new PhotoRun(first.Id, "bad", null, [new ItemBox(3, 0.1, 0.1, 0.2, 0.2)]), CancellationToken.None))!.Error);
    }

    [Theory]
    [InlineData(2.4, 3.0)]
    [InlineData(3.0, 3.5)]
    [InlineData(4.0, 5.0)]
    [InlineData(6.0, 7.0)]
    [InlineData(3.0, 8.0)]
    [InlineData(8.0, 3.0)]
    public void Every_room_type_starts_with_a_door_windows_and_furniture_that_fit(double width, double length)
    {
        var catalog = new EmbeddedObjectCatalog();

        Assert.All(Enum.GetValues<RoomType>(), type =>
        {
            var items = StarterLayout.For(type, width, length, 2.6, catalog.ForRoom(type));
            var plan = NewPlan(width, length);

            // A valid plan: every window and door fits on its wall.
            plan.Update("", width, length, 2.6, Wall, Floor, FloorType.Wood, PlanWall.Back, items, Now);
            Assert.Single(items, i => i.Shape == ItemShape.Door);
            Assert.Contains(items, i => i.Shape == ItemShape.Window);
            var standing = items.Where(i => i.Wall is null).ToList();

            Assert.True(standing.Count >= (width * length >= 12 ? 4 : 2), $"{type} {width}×{length}: only {standing.Count} objects");

            // Everything is inside the walls, and nothing but rugs overlaps.
            var boxes = standing.Select(i =>
            {
                var (across, down) = i.Rotation % 180 == 0 ? (i.Width, i.Depth) : (i.Depth, i.Width);

                return (Item: i, X1: i.X - across / 2, Y1: i.Y - down / 2, X2: i.X + across / 2, Y2: i.Y + down / 2);
            }).ToList();

            Assert.All(boxes, b => Assert.True(b.X1 >= -1 && b.Y1 >= -1 && b.X2 <= width * 100 + 1 && b.Y2 <= length * 100 + 1, $"{type}: {b.Item.Label} is outside"));
            var solid = boxes.Where(b => b.Item.Shape != ItemShape.Rug).ToList();

            for (var i = 0; i < solid.Count; i++)
            {
                for (var j = i + 1; j < solid.Count; j++)
                {
                    var (a, b) = (solid[i], solid[j]);

                    Assert.False(a.X1 < b.X2 - 1 && b.X1 < a.X2 - 1 && a.Y1 < b.Y2 - 1 && b.Y1 < a.Y2 - 1, $"{type} {width}×{length}: {a.Item.Label} overlaps {b.Item.Label}");
                }
            }
        });
    }

    [Fact]
    public void Every_room_type_can_be_furnished_down_to_the_smallest_walls()
    {
        var catalog = new EmbeddedObjectCatalog();
        var sizes = new[] { 1.0, 1.2, 1.5, 1.8, 2.0, 2.4 };

        foreach (var type in Enum.GetValues<RoomType>())
        {
            foreach (var width in sizes)
            {
                foreach (var length in sizes.Append(6.0))
                {
                    var items = StarterLayout.For(type, width, length, 2.6, catalog.ForRoom(type));
                    var plan = NewPlan(width, length);

                    // Whatever fits is placed, inside the walls, and the plan takes it.
                    plan.Update("", width, length, 2.6, Wall, Floor, FloorType.Wood, PlanWall.Back, items, Now);
                    Assert.All(items.Where(i => i.Wall is null), i =>
                    {
                        var (across, down) = i.Rotation % 180 == 0 ? (i.Width, i.Depth) : (i.Depth, i.Width);

                        Assert.True(i.X - across / 2 >= -1 && i.Y - down / 2 >= -1 && i.X + across / 2 <= width * 100 + 1 && i.Y + down / 2 <= length * 100 + 1,
                            $"{type} {width}×{length}: {i.Label} is outside");
                    });
                }
            }
        }
    }

    [Fact]
    public void A_room_starts_with_the_daylight_rules_glass_and_its_main_furniture()
    {
        var catalog = new EmbeddedObjectCatalog();

        var living = StarterLayout.For(RoomType.LivingRoom, 4, 5, 2.6, catalog.ForRoom(RoomType.LivingRoom));
        var glass = living.Where(i => i.Shape == ItemShape.Window).Sum(i => i.Width * i.Height);

        Assert.True(glass >= 4 * 5 * 10000 * 0.09, $"{glass} cm² of glass");
        Assert.All(living.Where(i => i.Shape == ItemShape.Window), w => Assert.Equal(((int)PlanWall.Back, 90.0), (w.Wall!.Value, w.Elevation)));
        Assert.Contains(living, i => i.Label == "three-seat sofa");
        Assert.Contains(living, i => i.Label == "coffee table");

        var bedroom = StarterLayout.For(RoomType.Bedroom, 4, 4, 2.6, catalog.ForRoom(RoomType.Bedroom));

        Assert.Contains(bedroom, i => i.Shape == ItemShape.Bed && i.Width == 160);
        Assert.Equal(2, bedroom.Count(i => i.Label == "nightstand"));
        var smallBedroom = StarterLayout.For(RoomType.Bedroom, 2.6, 2.4, 2.6, catalog.ForRoom(RoomType.Bedroom));

        Assert.Contains(smallBedroom, i => i.Shape == ItemShape.Bed && i.Width < 160);

        var dining = StarterLayout.For(RoomType.DiningRoom, 4, 5, 2.6, catalog.ForRoom(RoomType.DiningRoom));

        Assert.True(dining.Count(i => i.Label == "dining chair" || i.Shape == ItemShape.Chair) >= 4);
        var kitchen = StarterLayout.For(RoomType.Kitchen, 4, 4.5, 2.6, catalog.ForRoom(RoomType.Kitchen));

        Assert.Contains(kitchen, i => i.Label == "kitchen island" || i.Shape == ItemShape.Table);
        Assert.All(kitchen.Where(i => i.Shape == ItemShape.Window), w => Assert.Equal(110.0, w.Elevation));
    }

    [Fact]
    public async Task A_new_room_is_furnished_unless_the_user_wants_it_empty()
    {
        var create = new CreatePlan(_plans, new FixedUser("user-1"), new FixedClock(Now), new EmbeddedObjectCatalog(), new PlanDesigns(_designs, _plans, new FixedClock(Now)));

        var furnished = (await create.HandleAsync("", RoomType.Bedroom, 4, 4, 2.6, Wall, Floor, FloorType.Wood, AiMode.Fake, true, null, null, null, CancellationToken.None)).Value;
        var empty = (await create.HandleAsync("", RoomType.Bedroom, 4, 4, 2.6, Wall, Floor, FloorType.Wood, AiMode.Fake, false, null, null, null, CancellationToken.None)).Value;

        Assert.Contains(furnished.Items, i => i.Shape == ItemShape.Bed);
        Assert.Empty(empty.Items);
    }

    [Fact]
    public void Corner_and_round_objects_suit_their_shape_are_stored_and_described()
    {
        var grey = HexColor.Parse("#8A8580");
        var corner = new PlanItem("L-shaped corner sofa", ItemShape.Sofa, 200, 100, 260, 160, 85, 0, grey, form: ItemForm.LShaped);
        var turned = new PlanItem("L-shaped corner sofa", ItemShape.Sofa, 200, 300, 260, 160, 85, 90, grey, form: ItemForm.LShaped);
        var rug = new PlanItem("round rug", ItemShape.Rug, 200, 250, 200, 200, 1, 0, HexColor.Parse("#D8C3A5"), form: ItemForm.Round);
        var table = new PlanItem("oval coffee table", ItemShape.Table, 100, 250, 120, 70, 40, 0, HexColor.Parse("#B8864B"), form: ItemForm.Round);

        // A chair can't be an L, a sofa can't be round, and a window is always a rectangle.
        Assert.Throws<DomainException>(() => new PlanItem("chair", ItemShape.Chair, 0, 0, 45, 50, 85, 0, grey, form: ItemForm.LShaped));
        Assert.Throws<DomainException>(() => new PlanItem("sofa", ItemShape.Sofa, 0, 0, 200, 90, 85, 0, grey, form: ItemForm.Round));
        Assert.Throws<DomainException>(() => new PlanItem("desk", ItemShape.Table, 0, 0, 160, 140, 75, 0, grey, form: ItemForm.PShaped));
        var bigSofa = new PlanItem("P-shaped corner sofa", ItemShape.Sofa, 200, 250, 300, 200, 85, 0, grey, form: ItemForm.PShaped);

        Assert.Throws<DomainException>(() => new PlanItem("window", ItemShape.Window, 100, 0, 120, 10, 140, 0, grey, (int)PlanWall.Back, 90, ItemForm.LShaped));
        // Only a window is arched, and only a box is a shower's glass.
        Assert.Equal(ItemForm.Arched, new PlanItem("arched window", ItemShape.Window, 100, 0, 100, 10, 170, 0, grey, (int)PlanWall.Back, 80, ItemForm.Arched).Form);
        Assert.Equal(ItemForm.GlassCubicle, new PlanItem("glass shower cubicle", ItemShape.Box, 50, 50, 90, 90, 200, 0, grey, form: ItemForm.GlassCubicle).Form);
        Assert.Throws<DomainException>(() => new PlanItem("door", ItemShape.Door, 100, 0, 90, 5, 210, 0, grey, (int)PlanWall.Back, 0, ItemForm.Arched));
        Assert.Throws<DomainException>(() => new PlanItem("bed", ItemShape.Bed, 100, 100, 160, 200, 50, 0, grey, form: ItemForm.GlassScreen));
        // A swivel office chair is a chair.
        Assert.Equal(ItemForm.Swivel, new PlanItem("office chair", ItemShape.Chair, 100, 100, 52, 61, 115, 0, grey, form: ItemForm.Swivel).Form);
        Assert.Throws<DomainException>(() => new PlanItem("sofa", ItemShape.Sofa, 100, 100, 200, 90, 85, 0, grey, form: ItemForm.Swivel));

        IReadOnlyList<PlanItem> items = [corner, rug, Sofa()];
        var json = (string)PlanItemsJson.Converter.ConvertToProvider(items)!;

        Assert.Equal(items, (IReadOnlyList<PlanItem>)PlanItemsJson.Converter.ConvertFromProvider(json)!);
        Assert.Equal(2, json.Split("\"form\"").Length - 1);

        var plan = NewPlan();

        plan.Update("", 4, 5, 2.6, Wall, Floor, FloorType.Wood, PlanWall.Back, [corner, turned, rug, table, bigSofa], Now);
        var text = PlanPrompt.Write(plan);

        // The leg is at the right end before turning: 80 cm parts, its middle 90 cm right of and 40 cm in front of the middle.
        Assert.Contains("260 × 160 × 85 cm, L-shaped (its back run, across its whole width, is 80 cm deep; its corner leg is 80 cm wide, its middle 290 cm from the left wall and 140 cm from the far wall), facing the camera", text, StringComparison.Ordinal);
        Assert.Contains("its corner leg is 80 cm wide, its middle 160 cm from the left wall and 390 cm from the far wall", text, StringComparison.Ordinal);
        Assert.Contains("200 × 200 cm, flat on the floor, round seen from above", text, StringComparison.Ordinal);
        Assert.Contains("120 × 70 × 40 cm, oval seen from above", text, StringComparison.Ordinal);
        Assert.Contains("300 × 200 × 85 cm, P-shaped (its back run, across its whole width, is 80 cm deep; its corner leg is 80 cm wide, its middle 310 cm from the left wall and 250 cm from the far wall; at its front end the leg widens into a chaise 136 × 80 cm, its middle 282 cm from the left wall and 310 cm from the far wall)", text, StringComparison.Ordinal);

        // Each part resizes on its own, inside the object; the usual parts come with a new corner object and are stored.
        Assert.Equal(new ItemParts(80, 80, 136, 80), bigSofa.Parts);
        var resized = new PlanItem("P-shaped corner sofa", ItemShape.Sofa, 200, 250, 300, 200, 85, 0, grey, form: ItemForm.PShaped, parts: new ItemParts(95, 70, 160, 90));

        Assert.Contains("its back run, across its whole width, is 95 cm deep; its corner leg is 70 cm wide",
            PlanPrompt.Write(UpdatedWith(resized)), StringComparison.Ordinal);
        Assert.Throws<DomainException>(() => new PlanItem("sofa", ItemShape.Sofa, 0, 0, 300, 200, 85, 0, grey, form: ItemForm.PShaped, parts: new ItemParts(150, 70, 160, 90)));
        Assert.Throws<DomainException>(() => new PlanItem("sofa", ItemShape.Sofa, 0, 0, 300, 200, 85, 0, grey, form: ItemForm.PShaped, parts: new ItemParts(95, 170, 160, 90)));
        Assert.Throws<DomainException>(() => new PlanItem("sofa", ItemShape.Sofa, 0, 0, 220, 95, 85, 0, grey, parts: new ItemParts(50, 50)));
        var mirrored = new PlanItem("L-shaped corner sofa", ItemShape.Sofa, 200, 100, 260, 160, 85, 0, grey, form: ItemForm.LShaped, parts: new ItemParts(80, 80, LegLeft: true));

        // Mirrored, the leg is at the back run's left end: its middle 90 cm left of the sofa's.
        Assert.Contains("its corner leg is 80 cm wide, its middle 110 cm from the left wall and 140 cm from the far wall", PlanPrompt.Write(UpdatedWith(mirrored)),
            StringComparison.Ordinal);
        var stored = (string)PlanItemsJson.Converter.ConvertToProvider(new List<PlanItem> { resized, mirrored })!;

        Assert.Equal([resized, mirrored], (IReadOnlyList<PlanItem>)PlanItemsJson.Converter.ConvertFromProvider(stored)!);
    }

    /// <summary>A new living room holding one object.</summary>
    /// <param name="item">The object.</param>
    private static RoomPlan UpdatedWith(PlanItem item)
    {
        var plan = NewPlan();

        plan.Update("", 4, 5, 2.6, Wall, Floor, FloorType.Wood, PlanWall.Back, [item], Now);

        return plan;
    }

    [Fact]
    public void The_prompt_carries_the_style_and_the_users_own_words()
    {
        var plan = NewPlan();

        plan.Update("", 4, 5, 2.6, Wall, Floor, FloorType.Wood, PlanWall.Back, [Sofa()], Now);

        var text = PlanPrompt.Write(plan, PhotoStyle.Boho, " evening light, candles on the table ");

        // The style names no colors, so it can't recolor the objects. It and the user's own words come right after the
        // opening (at the end they were ignored), and the objects' colors are confirmed last.
        Assert.Contains("the color it has in the render. Style every object as boho: rattan and cane panels, woven textures, macramé and fringes, turned wood. Give each object the handles, legs, fronts, trims and textures of this style, so the style is clearly visible, while it keeps its place, outline and size. Each object's own color comes first: where the style's usual materials or finishes would change it, the object keeps its color and takes only the style's shapes and details. Appliances are not styled. Whatever the style, the sofa stays warm grey. The photo must also show: evening light, candles on the table. The room is ", text, StringComparison.Ordinal);
        // An appliance is marked so the style leaves it plain; other objects are not.
        var fridge = new PlanItem("American fridge freezer", ItemShape.Box, 300, 40, 90, 70, 180, 0, HexColor.Parse("#C8C8C8"));

        plan.Update("", 4, 5, 2.6, Wall, Floor, FloorType.Wood, PlanWall.Back, [Sofa(), fridge], Now);
        var withFridge = PlanPrompt.Write(plan, PhotoStyle.Boho);

        Assert.Contains("90 × 70 × 180 cm, a plain modern appliance in this color, not styled, facing the camera", withFridge, StringComparison.Ordinal);
        Assert.Single(withFridge.Split("not styled,").Skip(1));
        // With a style, the room's own appliances and colors are named right after it.
        Assert.Contains("Whatever the style, the American fridge freezer stays a plain, modern appliance. Whatever the style, the sofa stays warm grey.", withFridge, StringComparison.Ordinal);
        Assert.DoesNotContain("Whatever the style", PlanPrompt.Write(plan), StringComparison.Ordinal);
        plan.Update("", 4, 5, 2.6, Wall, Floor, FloorType.Wood, PlanWall.Back, [Sofa()], Now);
        Assert.EndsWith("Every object keeps exactly the color listed for it and shown in the render.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Style every object", PlanPrompt.Write(plan), StringComparison.Ordinal);

        // Without decorations the photo holds only what the render does; with them, only those are added, each named on
        // its object (a window's curtains too). Decorations are trimmed, distinct, short and few, and they are stored.
        Assert.Contains("Everything in the photo is already in the render: keep the rest of the room empty", text, StringComparison.Ordinal);
        Assert.DoesNotContain("decorat", text, StringComparison.Ordinal);
        var dressed = new PlanItem("sofa", ItemShape.Sofa, 200, 80, 220, 95, 85, 0, HexColor.Parse("#8A8580"), decorations: new ItemDecorations([" cushions ", "throw blanket", "open book"]));
        var curtained = new PlanItem("window", ItemShape.Window, 100, 0, 120, 10, 140, 0, HexColor.Parse("#F4F1EA"), (int)PlanWall.Back, 90, decorations: new ItemDecorations(["sheer curtains"]));

        plan.Update("", 4, 5, 2.6, Wall, Floor, FloorType.Wood, PlanWall.Back, [dressed, curtained], Now);
        var decorated = PlanPrompt.Write(plan);

        Assert.Contains("apart from the decorations named with their objects below, each resting on or hanging at the one object it is named with and on no other object", decorated, StringComparison.Ordinal);
        Assert.Contains("facing the camera, decorated with cushions, throw blanket and open book.", decorated, StringComparison.Ordinal);
        Assert.Contains("its bottom 90 cm above the floor, decorated with sheer curtains.", decorated, StringComparison.Ordinal);
        // Decorations on an object the camera can't see are left out, and so is the sentence about them.
        var behind = new PlanItem("sofa", ItemShape.Sofa, 200, 470, 220, 50, 85, 0, HexColor.Parse("#8A8580"), decorations: new ItemDecorations(["cushions"]));

        plan.Update("", 4, 5, 2.6, Wall, Floor, FloorType.Wood, PlanWall.Back, [behind, Sofa()], Now);
        Assert.DoesNotContain("decorat", PlanPrompt.Write(plan), StringComparison.Ordinal);
        Assert.Throws<DomainException>(() => new ItemDecorations(["cushions", "Cushions"]));
        Assert.Throws<DomainException>(() => new ItemDecorations(["  "]));
        Assert.Throws<DomainException>(() => new ItemDecorations([new string('x', 61)]));
        Assert.Throws<DomainException>(() => new ItemDecorations(Enumerable.Range(1, 9).Select(n => $"thing {n}")));
        IReadOnlyList<PlanItem> both = [dressed, curtained, Sofa()];
        var storedJson = (string)PlanItemsJson.Converter.ConvertToProvider(both)!;

        Assert.Equal(both, (IReadOnlyList<PlanItem>)PlanItemsJson.Converter.ConvertFromProvider(storedJson)!);
        Assert.Equal(2, storedJson.Split("\"decorations\"").Length - 1);
    }

    /// <summary>The fake AI with the room builder's fakes.</summary>
    /// <param name="images">The image model, if not the fake one.</param>
    private static FixedAiProviders Ai(IImageGenerator? images = null)
    {
        return new FixedAiProviders(new FakeInventoryProvider(NoDelay), new FakeSegmentationProvider(NoDelay), null,
            new FakeRoomBuilderAi(NoDelay), images ?? new FakeImageGenerator(NoDelay));
    }

    /// <summary>The generate use case with the fake AI and a real photo processor.</summary>
    /// <param name="user">The current user.</param>
    /// <param name="images">The image model, if not the fake one.</param>
    private GeneratePlanRoom Generator(string user = "user-1", IImageGenerator? images = null)
    {
        var clock = new FixedClock(Now);
        var takes = new TakeStore(new SkiaPhotoProcessor(), _storage, clock);

        return new GeneratePlanRoom(_plans, _rooms, new FixedUser(user), Ai(images), takes,
            new PlanPhotos(_rooms, new FixedUser(user), new PlanDesigns(_designs, _plans, clock), new RoomRemover(_rooms, _storage, NullLogger<RoomRemover>.Instance), NullLogger<PlanPhotos>.Instance), clock);
    }
}
