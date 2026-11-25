using DesignApp.Application.Helpers;
using DesignApp.Application.Objects;
using DesignApp.Application.UseCases;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Objects;
using DesignApp.Infrastructure.Ai;
using DesignApp.Infrastructure.Ai.Fakes;
using DesignApp.Infrastructure.Photos;
using DesignApp.Tests;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;

using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests.Builder;

/// <summary>
/// A room's 360-degree photo: its prompt, making it as a new room (whose photo is the view straight ahead), saving more
/// views of it, and how views are cut out of it.
/// </summary>
public class PanoramaTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);
    private static readonly IOptions<AiOptions> NoDelay = Options.Create(new AiOptions { UseFakes = true });
    private static readonly HexColor Grey = HexColor.Parse("#8A8580");

    private readonly FakePlans _plans = new();
    private readonly FakeRooms _rooms = new();
    private readonly FakeDesigns _designs = new();
    private readonly FakeStorage _storage = new();
    private readonly FixedClock _clock = new(Now);
    private readonly RecordingImageGenerator _images = new();
    private readonly SkiaPanoramaImaging _imaging = new();
    private readonly byte[] _guide = TestImages.Create(512, 256, SKEncodedImageFormat.Png);

    [Fact]
    public void The_prompt_keeps_the_projection_and_names_every_object_with_its_color_and_width()
    {
        var plan = Plan();

        var prompt = PanoramaPrompt.Write(plan);

        Assert.Contains("360-degree equirectangular panoramic photograph", prompt, StringComparison.Ordinal);
        Assert.Contains("1. warm grey (#8A8580) sofa, 220 cm wide, decorated with cushions", prompt, StringComparison.Ordinal);
        Assert.Contains("400 cm by 500 cm", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_panorama_makes_a_room_whose_photo_is_the_view_straight_ahead()
    {
        var plan = Plan();

        var made = await Generator().HandleAsync(plan.Id, _guide, PhotoStyle.AsBuilt, null, [new ItemBox(0, 0.4, 0.5, 0.6, 0.7)], new OutlinePoint(200, 300), 0, null,
            CancellationToken.None);
        var room = made!.Value;
        var take = Assert.Single(_rooms.Takes);

        // The image model makes a 4K photo; it is stored whole, and the room's photo is cut from it.
        Assert.Equal(new PhotoSize(4096, 2048), Assert.Single(_images.Sizes));
        Assert.Equal((4096, 2048, new OutlinePoint(200, 300)), (room.Panorama!.Width, room.Panorama.Height, room.Panorama.Spot));
        Assert.True(_storage.Files.ContainsKey(room.Panorama.Key));
        Assert.Equal((1024, 768, "360° view, straight ahead"), (take.Width, take.Height, take.Note));
        Assert.Equal(take.Id, room.ChosenTakeId);
        Assert.Contains("360-degree", _images.Calls[0].Prompt, StringComparison.Ordinal);
        Assert.Equal("object1", Assert.Single(_images.Calls[0].Boxes!).Label);
        // A signed-in user's room goes into the plan's design, as a Generate run's does.
        Assert.Contains(Assert.Single(_designs.All).Rooms, r => r.RoomId == room.Id);
    }

    [Fact]
    public async Task A_bad_view_spot_or_box_is_refused_before_any_photo_is_made()
    {
        var plan = Plan();
        var generate = Generator();
        var spot = new OutlinePoint(200, 300);
        var square = TestImages.Create(256, 256, SKEncodedImageFormat.Png);
        var jpeg = TestImages.Create(512, 256);

        Assert.Equal(DesignApp.Application.Constants.Messages.PanoramaGuide,
            (await generate.HandleAsync(plan.Id, square, PhotoStyle.AsBuilt, null, null, spot, 0, null, CancellationToken.None))!.Error);
        Assert.Equal(DesignApp.Application.Constants.Messages.PanoramaGuide,
            (await generate.HandleAsync(plan.Id, jpeg, PhotoStyle.AsBuilt, null, null, spot, 0, null, CancellationToken.None))!.Error);
        Assert.Equal(DesignApp.Application.Constants.Messages.PanoramaSpot,
            (await generate.HandleAsync(plan.Id, _guide, PhotoStyle.AsBuilt, null, null, new OutlinePoint(-20, 300), 0, null, CancellationToken.None))!.Error);
        Assert.Equal(DesignApp.Application.Constants.Messages.PhotoBoxes,
            (await generate.HandleAsync(plan.Id, _guide, PhotoStyle.AsBuilt, null, [new ItemBox(5, 0.1, 0.1, 0.2, 0.2)], spot, 0, null, CancellationToken.None))!.Error);
        Assert.Equal(DesignApp.Application.Constants.Messages.ReplacedRoomMissing,
            (await generate.HandleAsync(plan.Id, _guide, PhotoStyle.AsBuilt, null, null, spot, 0, Guid.NewGuid(), CancellationToken.None))!.Error);
        Assert.Empty(_images.Calls);
        Assert.Null(await Generator("someone-else").HandleAsync(plan.Id, _guide, PhotoStyle.AsBuilt, null, null, spot, 0, null, CancellationToken.None));
    }

    [Fact]
    public async Task A_saved_view_is_cut_from_the_panorama_and_a_room_without_one_is_refused()
    {
        var plan = Plan();
        var room = (await Generator().HandleAsync(plan.Id, _guide, PhotoStyle.AsBuilt, null, null, new OutlinePoint(200, 300), 0, null, CancellationToken.None))!.Value;
        var save = new SaveRoomPanoramaView(_rooms, new FixedUser("user-1"), _storage, _imaging, Takes());

        var saved = await save.HandleAsync(room.Id, new PanoramaView(40, -5, 60), CancellationToken.None);

        // No AI: the view comes from the stored photo.
        Assert.Equal("360° view, 40° right", saved!.Value.Note);
        Assert.Equal(2, _rooms.Takes.Count);
        Assert.Single(_images.Calls);
        Assert.Equal(DesignApp.Application.Constants.Messages.PanoramaView,
            (await save.HandleAsync(room.Id, new PanoramaView(40, 0, 120), CancellationToken.None))!.Error);
        // A 60° lens is 77° wide at 4:3, so turned 170° its edge would cross the seam behind.
        Assert.Equal(DesignApp.Application.Constants.Messages.PanoramaView,
            (await save.HandleAsync(room.Id, new PanoramaView(-170, 0, 60), CancellationToken.None))!.Error);
        Assert.Null(await save.HandleAsync(Guid.NewGuid(), new PanoramaView(0, 0, 60), CancellationToken.None));

        var other = Plan();
        var photo = (await new GeneratePlanRoom(_plans, _rooms, new FixedUser("user-1"), Ai(), Takes(), Photos("user-1"), new SkiaPhotoColors(NullLogger<SkiaPhotoColors>.Instance), _clock)
            .HandleAsync(other.Id, TestImages.Create(1280, 960), null, PhotoStyle.AsBuilt, null, null, null, null, new PhotoRun(null, "one"), CancellationToken.None))!.Value;

        Assert.Equal(DesignApp.Application.Constants.Messages.NoPanorama,
            (await save.HandleAsync(photo.Id, new PanoramaView(0, 0, 60), CancellationToken.None))!.Error);
    }

    [Fact]
    public async Task The_first_view_looks_where_the_panorama_starts_and_a_start_facing_the_seam_is_refused()
    {
        var plan = Plan();
        var generate = Generator();

        var room = (await generate.HandleAsync(plan.Id, _guide, PhotoStyle.AsBuilt, null, null, new OutlinePoint(200, 300), 45, null, CancellationToken.None))!.Value;

        Assert.Equal(45, room.Panorama!.StartYaw);
        Assert.Equal("360° view, 45° right", Assert.Single(_rooms.Takes).Note);
        Assert.Equal(DesignApp.Application.Constants.Messages.PanoramaStart,
            (await generate.HandleAsync(plan.Id, _guide, PhotoStyle.AsBuilt, null, null, new OutlinePoint(200, 300), 150, null, CancellationToken.None))!.Error);
    }

    [Fact]
    public async Task Deleting_a_room_deletes_its_panorama_too()
    {
        var plan = Plan();
        var room = (await Generator().HandleAsync(plan.Id, _guide, PhotoStyle.AsBuilt, null, null, new OutlinePoint(200, 300), 0, null, CancellationToken.None))!.Value;
        var key = room.Panorama!.Key;

        Assert.True(_storage.Files.ContainsKey(key));
        await new RoomRemover(_rooms, _storage, NullLogger<RoomRemover>.Instance).DeleteAsync([room], CancellationToken.None);

        Assert.False(_storage.Files.ContainsKey(key));
    }

    [Theory]
    [InlineData(45, 0, "blue")]
    [InlineData(-45, 0, "green")]
    [InlineData(135, 0, "yellow")]
    [InlineData(-135, 0, "red")]
    [InlineData(0, 80, "white")]
    public void A_view_looks_where_it_is_turned_and_tilted(double yaw, double pitch, string expected)
    {
        // Columns by quarter (behind-left, left, right, behind-right) and a white band across the top.
        var colors = new Dictionary<string, SKColor>
        {
            ["red"] = new(220, 30, 30), ["green"] = new(30, 200, 60), ["blue"] = new(30, 60, 220), ["yellow"] = new(230, 210, 40), ["white"] = SKColors.White,
        };
        using var bitmap = new SKBitmap(720, 360);

        using (var canvas = new SKCanvas(bitmap))
        {
            string[] quarters = ["red", "green", "blue", "yellow"];

            for (var q = 0; q < 4; q++)
            {
                using var paint = new SKPaint { Color = colors[quarters[q]] };

                canvas.DrawRect(q * 180, 0, 180, 360, paint);
            }

            using var top = new SKPaint { Color = SKColors.White };

            canvas.DrawRect(0, 0, 720, 30, top);
        }

        using var encoded = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        var cut = _imaging.Cut(encoded.ToArray(), new PanoramaView(yaw, pitch, 20), 40, 30);
        using var view = SKBitmap.Decode(cut);
        var middle = view.GetPixel(20, 15);
        var nearest = colors.MinBy(c => Math.Abs(c.Value.Red - middle.Red) + Math.Abs(c.Value.Green - middle.Green) + Math.Abs(c.Value.Blue - middle.Blue)).Key;

        Assert.Equal(expected, nearest);
    }

    /// <summary>A 4 x 5 m living room with a decorated sofa, owned by user-1.</summary>
    private RoomPlan Plan()
    {
        var plan = RoomPlan.Create(Guid.CreateVersion7(Now), "user-1", "", RoomType.LivingRoom, 4, 5, 2.6, HexColor.Parse("#E8E2D6"), HexColor.Parse("#B89B7A"),
            FloorType.Wood, AiMode.Fake, Now);

        plan.Update("", 4, 5, 2.6, plan.WallColor, plan.FloorColor, FloorType.Wood, PlanWall.Back,
            [new PlanItem("sofa", ItemShape.Sofa, 200, 80, 220, 95, 85, 0, Grey, decorations: new ItemDecorations(["cushions"]))], Now);
        _plans.Add(plan);

        return plan;
    }

    /// <summary>The fake AI with the recording image model.</summary>
    private FixedAiProviders Ai()
    {
        return new FixedAiProviders(new FakeInventoryProvider(NoDelay), new FakeSegmentationProvider(NoDelay), null, null, _images);
    }

    /// <summary>The photo store over the fake storage and a real photo processor.</summary>
    private TakeStore Takes()
    {
        return new TakeStore(new SkiaPhotoProcessor(), _storage, _clock);
    }

    /// <summary>The shared plan photo helpers for a user, over the fakes.</summary>
    /// <param name="user">The current user.</param>
    private PlanPhotos Photos(string user)
    {
        return new PlanPhotos(_rooms, new FixedUser(user), new PlanDesigns(_designs, _plans, _clock), new RoomRemover(_rooms, _storage, NullLogger<RoomRemover>.Instance), NullLogger<PlanPhotos>.Instance);
    }

    /// <summary>The panorama use case for a user.</summary>
    /// <param name="user">The current user.</param>
    private GeneratePlanPanorama Generator(string user = "user-1")
    {
        return new GeneratePlanPanorama(_plans, _rooms, new FixedUser(user), Ai(), _imaging, _storage, Takes(), Photos(user), _clock);
    }
}
