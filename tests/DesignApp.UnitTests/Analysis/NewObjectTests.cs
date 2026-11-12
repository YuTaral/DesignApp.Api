using DesignApp.Application.Objects;
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
using SkiaSharp;
using static DesignApp.Application.Constants;
using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests.Analysis;

/// <summary>
/// Objects the user marks by hand: the mask on its own, the room's rules for adding one, and the use case with
/// stored label map versions.
/// </summary>
public class NewObjectTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);
    private static readonly IOptions<AiOptions> NoDelay = Options.Create(new AiOptions { UseFakes = true });

    private readonly FakeRooms _rooms = new();
    private readonly FakeStorage _storage = new();

    // A 20x10 label map: object 1 covers x < 6, object 2 covers x >= 10, x 6..9 is unassigned.
    private const int W = 20, H = 10;

    private static byte[] Labels()
    {
        var labels = new byte[W * H];
        for (var p = 0; p < W * H; p++)
            labels[p] = (byte)(p % W < 6 ? 1 : p % W >= 10 ? 2 : 0);
        return labels;
    }

    /// <summary>A stroke through label-map pixel centers.</summary>
    private static MarkStroke Stroke(OutlineBrushMode mode, params (double X, double Y)[] pixels)
    {
        return new(mode, 0.6, [.. pixels.Select(p => new StrokePoint((p.X + 0.5) / W, (p.Y + 0.5) / H))]);
    }

    [Fact]
    public void Marked_pixels_become_the_new_object_on_top_of_others()
    {
        var labels = Labels();

        // A line across the gap and into object 2 at row 5.
        var marked = ObjectMarker.Apply(labels, W, H, new NewObjectMark(3, [Stroke(OutlineBrushMode.Paint, (7, 5), (12, 5))]));

        Assert.Equal(6, marked.Value);
        Assert.All(Enumerable.Range(7, 6), x => Assert.Equal(3, labels[5 * W + x]));
        Assert.Equal(2, labels[4 * W + 12]); // the row above is untouched
    }

    [Fact]
    public void Unmarking_takes_pixels_out_of_the_mark_and_leaves_the_room_as_it_was()
    {
        var labels = Labels();
        var mark = new NewObjectMark(3, [Stroke(OutlineBrushMode.Paint, (7, 5), (12, 5)), Stroke(OutlineBrushMode.Erase, (11, 5), (12, 5))]);

        var marked = ObjectMarker.Apply(labels, W, H, mark);

        Assert.Equal(4, marked.Value);
        Assert.Equal(3, labels[5 * W + 10]);
        Assert.Equal(2, labels[5 * W + 11]); // unmarked: still object 2's, not unassigned
    }

    [Fact]
    public void A_mark_that_leaves_nothing_is_refused()
    {
        var labels = Labels();
        var mark = new NewObjectMark(3, [Stroke(OutlineBrushMode.Paint, (7, 5)), Stroke(OutlineBrushMode.Erase, (7, 5))]);

        var marked = ObjectMarker.Apply(labels, W, H, mark);

        Assert.False(marked.Succeeded);
        Assert.Equal(Labels(), labels);
    }

    [Fact]
    public void Being_marked_by_hand_is_stored_and_older_objects_read_as_found_by_the_ai()
    {
        var box = new BoundingBox(0.1, 0.1, 0.2, 0.2);
        IReadOnlyList<DetectedObject> objects =
        [
            new(1, "sofa", ObjectCategory.Furniture, box, HexColor.Parse("#EEEEEE")),
            new(2, "soap bottle", ObjectCategory.Decor, box, HexColor.Parse("#AA5500"), MarkedByHand: true),
        ];

        var json = (string)DesignApp.Infrastructure.Database.DetectedObjectsJson.Converter.ConvertToProvider(objects)!;
        var back = (IReadOnlyList<DetectedObject>)DesignApp.Infrastructure.Database.DetectedObjectsJson.Converter.ConvertFromProvider(json)!;
        // A room stored before the flag existed has no "hand" field at all.
        var stored = System.Text.RegularExpressions.Regex.Replace(json, ",\"hand\":(true|false)", "");
        var old = (IReadOnlyList<DetectedObject>)DesignApp.Infrastructure.Database.DetectedObjectsJson.Converter.ConvertFromProvider(stored)!;

        Assert.Equal([false, true], back.Select(o => o.MarkedByHand));
        Assert.DoesNotContain("hand", stored, StringComparison.Ordinal);
        Assert.Equal([false, false], old.Select(o => o.MarkedByHand));
    }

    [Fact]
    public void A_room_only_takes_its_next_index_and_only_when_ready()
    {
        var id = Guid.CreateVersion7(Now);
        var room = Room.Upload(id, "user-1", "photo", 600, 400, Now);
        var lamp = new DetectedObject(1, "lamp", ObjectCategory.Lighting, new BoundingBox(0, 0, 0.1, 0.1), HexColor.Parse("#FFFFFF"));

        Assert.Throws<DomainException>(() => room.AddObject(lamp, 0, r => $"labels/{r}"));
        Assert.Equal(1, room.NextObjectIndex);
    }

    [Fact]
    public async Task A_new_object_is_stored_as_a_new_version_and_undo_can_go_back()
    {
        var room = await AnalyzedRoomAsync();
        var imaging = new SkiaRoomImaging();
        var adder = new AddRoomObject(_rooms, new FixedUser("user-1"), _storage, imaging);
        var count = room.Objects.Count;
        var next = room.NextObjectIndex;
        var (width, height) = (600, 400);
        MarkStroke[] dab = [new(OutlineBrushMode.Paint, 5, [new((100 + 0.5) / width, (100 + 0.5) / height)])];

        var result = await adder.AddAsync(room.Id, 0, "  side lamp ", ObjectCategory.Lighting, dab, CancellationToken.None);

        Assert.True(result!.Succeeded, result.Error);
        var added = room.Objects[^1];
        Assert.Equal((count + 1, next, "side lamp", ObjectCategory.Lighting, true), (room.Objects.Count, added.Index, added.Label, added.Category, added.MarkedByHand));
        Assert.Equal((1, 1), (room.LabelMapRevision, room.LatestLabelMapRevision));
        Assert.InRange(added.Box.X, 94.0 / width, 96.0 / width);
        Assert.InRange(added.Box.Width, 9.0 / width, 11.0 / width);
        var labels = DecodeLabels(_storage.Files[room.LabelMapKey!].Content);
        Assert.Equal(next, labels[100 * width + 100]);
        Assert.DoesNotContain((byte)next, DecodeLabels(_storage.Files[DesignApp.Application.Constants.Rooms.LabelMapKey(room.Id, 0)].Content));

        // Undo shows version 0 again; the object stays listed, with no pixels there, and redo brings it back.
        var undo = new FixRoomOutline(_rooms, new FixedUser("user-1"), _storage, imaging);
        Assert.True((await undo.UseRevisionAsync(room.Id, 0, CancellationToken.None))!.Succeeded);
        Assert.Equal(count + 1, room.Objects.Count);

        // A second object on version 0 never reuses the first one's index.
        var second = await adder.AddAsync(room.Id, 0, "picture", ObjectCategory.Decor, dab, CancellationToken.None);
        Assert.True(second!.Succeeded, second.Error);
        Assert.Equal(next + 1, room.Objects[^1].Index);
    }

    [Theory]
    [InlineData("", ObjectCategory.Other, 1, "name")]
    [InlineData("a name far too long for any object in a room, much longer than sixty", ObjectCategory.Other, 1, "name")]
    [InlineData("lamp", (ObjectCategory)99, 1, "type")]
    [InlineData("lamp", ObjectCategory.Other, 0, "strokes")]
    [InlineData("lamp", ObjectCategory.Other, 101, "strokes")]
    public async Task Bad_requests_are_refused_with_a_reason(string label, ObjectCategory category, int strokes, string message)
    {
        var room = await AnalyzedRoomAsync();
        var adder = new AddRoomObject(_rooms, new FixedUser("user-1"), _storage, new SkiaRoomImaging());
        var list = Enumerable.Range(0, strokes).Select(_ => new MarkStroke(OutlineBrushMode.Paint, 5, [new(0.5, 0.5)])).ToList();

        var result = await adder.AddAsync(room.Id, 0, label, category, list, CancellationToken.None);

        Assert.False(result!.Succeeded);
        Assert.Contains(message, result.Error, StringComparison.Ordinal);
        Assert.Equal(0, room.LatestLabelMapRevision);
    }

    [Fact]
    public async Task Other_users_and_unknown_versions_are_refused()
    {
        var room = await AnalyzedRoomAsync();
        var imaging = new SkiaRoomImaging();
        MarkStroke[] dab = [new(OutlineBrushMode.Paint, 5, [new(0.5, 0.5)])];

        Assert.Null(await new AddRoomObject(_rooms, new FixedUser("someone-else"), _storage, imaging)
            .AddAsync(room.Id, 0, "lamp", ObjectCategory.Other, dab, CancellationToken.None));
        Assert.False((await new AddRoomObject(_rooms, new FixedUser("user-1"), _storage, imaging)
            .AddAsync(room.Id, 3, "lamp", ObjectCategory.Other, dab, CancellationToken.None))!.Succeeded);
    }

    /// <summary>Uploads and analyzes a room with the fake AI and a real photo.</summary>
    private async Task<Room> AnalyzedRoomAsync()
    {
        var id = Guid.CreateVersion7(Now);
        var room = Room.Upload(id, "user-1", DesignApp.Application.Constants.Rooms.PhotoKey(id), 600, 400, Now);
        _storage.Files[room.PhotoKey] = (TestImages.Create(600, 400), DesignApp.Application.Constants.ContentTypes.Jpeg);
        _rooms.Add(room);
        await _rooms.SaveChangesAsync(CancellationToken.None);

        await new AnalyzeRoom(_rooms, _storage, new SkiaRoomImaging(),
            new FixedAiProviders(new FakeInventoryProvider(NoDelay), new FakeSegmentationProvider(NoDelay)),
            new RecordingNotifier(), NullLogger<AnalyzeRoom>.Instance)
            .HandleAsync(room.Id, CancellationToken.None);
        Assert.Equal(RoomStatus.Ready, room.Status);
        return room;
    }

    /// <summary>Decodes a label map PNG to one byte per pixel.</summary>
    /// <param name="png">The PNG.</param>
    private static byte[] DecodeLabels(byte[] png)
    {
        using var codec = SKCodec.Create(new MemoryStream(png));
        using var bitmap = SKBitmap.Decode(codec, new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Gray8, SKAlphaType.Opaque));
        return bitmap.GetPixelSpan().ToArray();
    }
}
