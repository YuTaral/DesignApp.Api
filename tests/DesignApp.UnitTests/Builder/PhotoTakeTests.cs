using DesignApp.Application.Helpers;
using DesignApp.Application.UseCases;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;
using DesignApp.Infrastructure.Ai;
using DesignApp.Infrastructure.Ai.Fakes;
using DesignApp.Infrastructure.Photos;
using DesignApp.Tests;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests.Builder;

/// <summary>
/// A Generate run's photos as one room: the room and its photos to choose from, the plan's own design, changes the AI
/// makes to a photo, and choosing which photo is the room's.
/// </summary>
public class PhotoTakeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);
    private static readonly IOptions<AiOptions> NoDelay = Options.Create(new AiOptions { UseFakes = true });

    private readonly FakePlans _plans = new();
    private readonly FakeRooms _rooms = new();
    private readonly FakeDesigns _designs = new();
    private readonly FakeStorage _storage = new();
    private readonly FixedClock _clock = new(Now);
    private readonly byte[] _guide = TestImages.Create(1280, 960);

    [Fact]
    public async Task A_runs_photos_are_one_room_its_first_photo_the_rooms()
    {
        var plan = Plan("user-1");
        var generate = Generator("user-1");

        var first = await generate.HandleAsync(plan.Id, _guide, PhotoStyle.AsBuilt, null, null, null, null, null, "Back wall, from the middle", CancellationToken.None);
        var room = first!.Value;
        var firstTake = Assert.Single(_rooms.Takes);

        Assert.Equal((plan.Id, firstTake.Id, RoomStatus.Waiting), (room.PlanId!.Value, room.ChosenTakeId!.Value, room.Status));
        Assert.Equal("Back wall, from the middle", firstTake.Note);
        Assert.True(_storage.Files.ContainsKey(firstTake.Key));
        Assert.Equal(_storage.Files[firstTake.Key].Content, _storage.Files[room.PhotoKey].Content);

        // The run's other photos go into the same room; the room keeps its photo.
        var second = await generate.HandleAsync(plan.Id, _guide, PhotoStyle.AsBuilt, null, null, null, null, room.Id, "Right wall", CancellationToken.None);

        Assert.Same(room, second!.Value);
        Assert.Equal(2, _rooms.Takes.Count);
        Assert.Equal(firstTake.Id, room.ChosenTakeId);
        Assert.Single(_rooms.Saved);

        // Another plan's room, and too long a note, are refused before any photo is made.
        var other = Plan("user-1");

        Assert.Equal(DesignApp.Application.Constants.Messages.NotThisPlansRoom,
            (await generate.HandleAsync(other.Id, _guide, PhotoStyle.AsBuilt, null, null, null, null, room.Id, null, CancellationToken.None))!.Error);
        Assert.Equal(DesignApp.Application.Constants.Messages.PhotoNote,
            (await generate.HandleAsync(plan.Id, _guide, PhotoStyle.AsBuilt, null, null, null, null, null, new string('x', 501), CancellationToken.None))!.Error);
    }

    [Fact]
    public async Task A_signed_in_users_runs_go_into_the_plans_own_design_and_a_guests_into_none()
    {
        var plan = Plan("user-1");
        var generate = Generator("user-1");

        var one = (await generate.HandleAsync(plan.Id, _guide, PhotoStyle.AsBuilt, null, null, null, null, null, "one", CancellationToken.None))!.Value;
        var two = (await generate.HandleAsync(plan.Id, _guide, PhotoStyle.AsBuilt, null, null, null, null, null, "two", CancellationToken.None))!.Value;
        var design = Assert.Single(_designs.All);

        Assert.Equal(design.Id, plan.DesignId);
        Assert.Equal("Living room", design.Name);
        Assert.Equal([one.Id, two.Id], design.Rooms.Select(r => r.RoomId));

        // A deleted design is made again; a room already in it isn't added twice.
        _designs.All.Clear();
        var saved = await new SaveRoomToPlanDesign(_rooms, _plans, Designs(), new FixedUser("user-1")).HandleAsync(one.Id, CancellationToken.None);

        Assert.Equal(Assert.Single(_designs.All).Id, saved!.Value);
        Assert.True((await new SaveRoomToPlanDesign(_rooms, _plans, Designs(), new FixedUser("user-1")).HandleAsync(one.Id, CancellationToken.None))!.Succeeded);
        Assert.Single(_designs.All[0].Rooms);

        var guest = GuestIds.New();
        var guestPlan = Plan(guest);

        Assert.True((await Generator(guest).HandleAsync(guestPlan.Id, _guide, PhotoStyle.AsBuilt, null, null, null, null, null, "", CancellationToken.None))!.Succeeded);
        Assert.Null(guestPlan.DesignId);
        Assert.Single(_designs.All);
    }

    [Fact]
    public async Task A_change_makes_a_new_photo_of_the_room_from_the_one_changed()
    {
        var room = (await Generator("user-1").HandleAsync(Plan("user-1").Id, _guide, PhotoStyle.AsBuilt, null, null, null, null, null, "view", CancellationToken.None))!.Value;
        var images = new RecordingImageGenerator();
        var change = new ChangeRoomTake(_rooms, new FixedUser("user-1"), Ai(images), Takes());
        var original = Assert.Single(_rooms.Takes);

        var made = await change.HandleAsync(room.Id, original.Id, "  make the sofa green velvet ", CancellationToken.None);

        Assert.True(made!.Succeeded, made.Error);
        Assert.Equal(("make the sofa green velvet", original.Id), (made.Value.Note, made.Value.ChangedFrom!.Value));
        Assert.Equal(2, _rooms.Takes.Count);
        Assert.Contains("make the sofa green velvet", Assert.Single(images.Calls).Prompt, StringComparison.Ordinal);
        Assert.Equal(original.Id, room.ChosenTakeId); // the room's photo stays as it is

        Assert.Equal(DesignApp.Application.Constants.Messages.ChangeWords, (await change.HandleAsync(room.Id, original.Id, " ", CancellationToken.None))!.Error);
        Assert.Equal(DesignApp.Application.Constants.Messages.ChangeWords, (await change.HandleAsync(room.Id, original.Id, new string('x', 501), CancellationToken.None))!.Error);
        Assert.Null(await change.HandleAsync(room.Id, Guid.NewGuid(), "warmer", CancellationToken.None));
        Assert.Null(await new ChangeRoomTake(_rooms, new FixedUser("someone-else"), Ai(images), Takes()).HandleAsync(room.Id, original.Id, "warmer", CancellationToken.None));
    }

    [Fact]
    public async Task Choosing_another_photo_makes_it_the_rooms_photo()
    {
        var plan = Plan("user-1");
        var generate = Generator("user-1");
        var room = (await generate.HandleAsync(plan.Id, _guide, PhotoStyle.AsBuilt, null, null, null, null, null, "one", CancellationToken.None))!.Value;
        var wide = TestImages.Create(1600, 900);

        await generate.HandleAsync(plan.Id, wide, PhotoStyle.AsBuilt, null, null, null, null, room.Id, "two", CancellationToken.None);
        var second = _rooms.Takes[1];
        var choose = new ChooseRoomTake(_rooms, _designs, new FixedUser("user-1"), Takes(), _storage, _clock, NullLogger<ChooseRoomTake>.Instance);

        var chosen = await choose.HandleAsync(room.Id, second.Id, CancellationToken.None);

        Assert.True(chosen!.Succeeded, chosen.Error);
        Assert.Equal((second.Id, second.Width, second.Height), (room.ChosenTakeId!.Value, room.Width, room.Height));
        Assert.Equal(_storage.Files[second.Key].Content, _storage.Files[room.PhotoKey].Content);

        // While its photo is being analyzed, the room keeps it.
        room.QueueAnalysis();
        Assert.Equal(Messages.CannotChooseTake(RoomStatus.Uploaded), (await choose.HandleAsync(room.Id, _rooms.Takes[0].Id, CancellationToken.None))!.Error);
        Assert.Null(await choose.HandleAsync(room.Id, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task A_photo_whose_analysis_failed_can_be_chosen_again_and_analyzed_afresh()
    {
        var plan = Plan("user-1");
        var room = (await Generator("user-1").HandleAsync(plan.Id, _guide, PhotoStyle.AsBuilt, null, null, null, null, null, "one", CancellationToken.None))!.Value;
        var take = Assert.Single(_rooms.Takes);
        var choose = new ChooseRoomTake(_rooms, _designs, new FixedUser("user-1"), Takes(), _storage, _clock, NullLogger<ChooseRoomTake>.Instance);

        room.QueueAnalysis();
        room.StartAnalysis();
        room.MarkFailed("The AI was down.");
        var set = room.PhotoSetId;

        var again = await choose.HandleAsync(room.Id, take.Id, CancellationToken.None);

        Assert.True(again!.Succeeded, again.Error);
        Assert.Equal((RoomStatus.Waiting, take.Id), (room.Status, room.ChosenTakeId!.Value));
        Assert.Null(room.FailureReason);
        Assert.NotEqual(set, room.PhotoSetId);
    }

    [Fact]
    public void An_analyzed_room_waits_for_its_analysis_again_with_another_photo()
    {
        var roomId = Guid.CreateVersion7(Now);
        var first = PhotoTake.Create(Guid.CreateVersion7(Now), roomId, "a.jpg", 800, 600, "one", null, Now);
        var second = PhotoTake.Create(Guid.CreateVersion7(Now.AddSeconds(1)), roomId, "b.jpg", 1024, 768, "two", null, Now);
        var room = Room.FromPlan(roomId, "user-1", "photo.jpg", first, Now, AiMode.Fake, Guid.NewGuid());

        room.QueueAnalysis();
        room.StartAnalysis();
        room.StartSegmentation();
        room.MarkReady([new DetectedObject(1, "sofa", ObjectCategory.Furniture, new BoundingBox(0.1, 0.1, 0.5, 0.5), HexColor.Parse("#808080"))], "labels.png");
        room.AddLabelMapRevision(0, r => $"labels-{r}.png");

        var firstSet = room.PhotoSetId;

        room.ChooseTake(second);

        Assert.Equal((RoomStatus.Waiting, 1024, 768, 0, 0), (room.Status, room.Width, room.Height, room.LatestLabelMapRevision, room.AnalysisAttempts));
        // Every choice gives the room's files new addresses, even of a photo chosen before (its new outlines differ).
        var secondSet = room.PhotoSetId;

        room.ChooseTake(first);
        Assert.Equal(3, new[] { firstSet, secondSet, room.PhotoSetId }.Distinct().Count());
        Assert.NotNull(firstSet);
        Assert.Empty(room.Objects);
        Assert.Null(room.LabelMapKey);
        Assert.Throws<DomainException>(() => room.ChooseTake(PhotoTake.Create(Guid.NewGuid(), Guid.NewGuid(), "c.jpg", 10, 10, "", null, Now)));
        Assert.Throws<DomainException>(() => PhotoTake.Create(Guid.NewGuid(), roomId, " ", 10, 10, "", null, Now));
    }

    /// <summary>A plan with a sofa, owned by a user.</summary>
    /// <param name="owner">The owner.</param>
    private RoomPlan Plan(string owner)
    {
        var plan = RoomPlan.Create(Guid.NewGuid(), owner, "", RoomType.LivingRoom, 4, 5, 2.6, HexColor.Parse("#E8E2D6"), HexColor.Parse("#B89B7A"), FloorType.Wood, AiMode.Fake, Now);

        plan.Update("", 4, 5, 2.6, plan.WallColor, plan.FloorColor, FloorType.Wood, PlanWall.Back,
            [new PlanItem("sofa", ItemShape.Sofa, 200, 80, 220, 95, 85, 0, HexColor.Parse("#8A8580"))], Now);
        _plans.Add(plan);

        return plan;
    }

    /// <summary>The fake AI, with an image model.</summary>
    /// <param name="images">The image model, if not the fake one.</param>
    private static FixedAiProviders Ai(Application.Interfaces.IImageGenerator? images = null)
    {
        return new FixedAiProviders(new FakeInventoryProvider(NoDelay), new FakeSegmentationProvider(NoDelay), null, null, images ?? new FakeImageGenerator(NoDelay));
    }

    /// <summary>The photo store over the fake storage and a real photo processor.</summary>
    private TakeStore Takes()
    {
        return new TakeStore(new SkiaPhotoProcessor(), _storage, _clock);
    }

    /// <summary>The plan's design helper over the fake designs.</summary>
    private PlanDesigns Designs()
    {
        return new PlanDesigns(_designs, _plans, _clock);
    }

    /// <summary>The generate use case for a user.</summary>
    /// <param name="user">The current user.</param>
    private GeneratePlanRoom Generator(string user)
    {
        return new GeneratePlanRoom(_plans, _rooms, new FixedUser(user), Ai(), Takes(), Designs(), _clock, NullLogger<GeneratePlanRoom>.Instance);
    }
}
