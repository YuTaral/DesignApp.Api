using DesignApp.Application.Objects;
using DesignApp.Application.UseCases;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Extensions;
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
/// Parts of objects drawn by hand (a chair's seat): marking and brushing only take the object's pixels, a part can be
/// merged back, and an object goes together with its parts.
/// </summary>
public class PartTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly IOptions<AiOptions> NoDelay = Options.Create(new AiOptions { UseFakes = true });
    private static readonly BoundingBox Box = new(0.1, 0.1, 0.2, 0.2);

    private readonly FakeRooms _rooms = new();
    private readonly FakeStorage _storage = new();

    // A 20x10 label map: object 1 covers x < 6, object 2 covers x >= 10, x 6..9 is unassigned.
    private const int W = 20, H = 10;

    private static byte[] Labels()
    {
        var labels = new byte[W * H];

        for (var p = 0; p < W * H; p++)
        {
            labels[p] = (byte)(p % W < 6 ? 1 : p % W >= 10 ? 2 : 0);
        }

        return labels;
    }

    /// <summary>A stroke through label-map pixel centers.</summary>
    private static MarkStroke Stroke(params (double X, double Y)[] pixels)
    {
        return new(OutlineBrushMode.Paint, 0.6, [.. pixels.Select(p => new StrokePoint((p.X + 0.5) / W, (p.Y + 0.5) / H))]);
    }

    [Fact]
    public void A_part_only_takes_its_objects_pixels()
    {
        var labels = Labels();

        // A line from inside object 1 across the gap at row 5: only object 1's pixels become the part.
        var marked = ObjectMarker.Apply(labels, W, H, new NewObjectMark(3, [Stroke((3, 5), (8, 5))], ParentIndex: 1, ParentLabel: "chair"));

        Assert.Equal(3, marked.Value);
        Assert.Equal([3, 3, 3, 0, 0, 0], Enumerable.Range(3, 6).Select(x => (int)labels[5 * W + x]));
    }

    [Fact]
    public void A_part_drawn_outside_its_object_is_refused()
    {
        var labels = Labels();

        var marked = ObjectMarker.Apply(labels, W, H, new NewObjectMark(3, [Stroke((7, 5), (8, 5))], ParentIndex: 1, ParentLabel: "chair"));

        Assert.False(marked.Succeeded);
        Assert.Equal(DesignApp.Infrastructure.Constants.Messages.PartOutsideParent("chair"), marked.Error);
        Assert.Equal(Labels(), labels);
    }

    [Fact]
    public void Brushing_a_part_takes_only_its_objects_pixels_and_erasing_gives_them_back()
    {
        var labels = Labels();

        labels[5 * W + 2] = 3; // the part, inside object 1

        var painted = OutlineBrush.Apply(labels, W, H, new OutlineStroke(3, "seat", OutlineBrushMode.Paint, 0.6,
            [new((0 + 0.5) / W, (5 + 0.5) / H), new((12 + 0.5) / W, (5 + 0.5) / H)], ParentIndex: 1));

        Assert.True(painted.Succeeded);
        Assert.Equal([3, 3, 3, 3, 3, 3, 0, 0, 0, 0, 2, 2, 2], Enumerable.Range(0, 13).Select(x => (int)labels[5 * W + x]));

        var erased = OutlineBrush.Apply(labels, W, H, new OutlineStroke(3, "seat", OutlineBrushMode.Erase, 0.6,
            [new((0 + 0.5) / W, (5 + 0.5) / H), new((3 + 0.5) / W, (5 + 0.5) / H)], ParentIndex: 1));

        Assert.True(erased.Succeeded);
        Assert.Equal([1, 1, 1, 1, 3, 3], Enumerable.Range(0, 6).Select(x => (int)labels[5 * W + x]));
    }

    [Fact]
    public void A_room_only_takes_parts_of_whole_objects_it_has()
    {
        var chair = new DetectedObject(1, "chair", ObjectCategory.Furniture, Box, HexColor.Parse("#806040"));
        var seat = new DetectedObject(2, "seat", ObjectCategory.Furniture, Box, HexColor.Parse("#A08060"), ParentIndex: 1);
        var cushion = new DetectedObject(3, "cushion", ObjectCategory.Furniture, Box, HexColor.Parse("#C0A080"), ParentIndex: 2);

        Assert.Throws<DomainException>(() => ReadyRoom([chair, cushion with { Index = 2, ParentIndex = 5 }]));
        var room = ReadyRoom([chair, seat]);

        Assert.Throws<DomainException>(() => room.AddObject(cushion, 0, r => $"labels/{r}"));
        Assert.Throws<DomainException>(() => room.MergePart(1, 0, r => $"labels/{r}"));
        Assert.Equal(1, room.MergePart(2, 0, r => $"labels/{r}"));
    }

    [Fact]
    public void Being_a_part_is_stored_and_older_objects_are_whole()
    {
        IReadOnlyList<DetectedObject> objects =
        [
            new(1, "chair", ObjectCategory.Furniture, Box, HexColor.Parse("#806040")),
            new(2, "seat", ObjectCategory.Furniture, Box, HexColor.Parse("#A08060"), MarkedByHand: true, ParentIndex: 1),
        ];

        var json = (string)DesignApp.Infrastructure.Database.DetectedObjectsJson.Converter.ConvertToProvider(objects)!;
        var back = (IReadOnlyList<DetectedObject>)DesignApp.Infrastructure.Database.DetectedObjectsJson.Converter.ConvertFromProvider(json)!;
        var old = (IReadOnlyList<DetectedObject>)DesignApp.Infrastructure.Database.DetectedObjectsJson.Converter.ConvertFromProvider(
            System.Text.RegularExpressions.Regex.Replace(json, ",\"parent\":(null|\\d+)", ""))!;

        Assert.Equal(objects, back);
        Assert.All(old, o => Assert.Null(o.ParentIndex));
    }

    [Fact]
    public async Task A_part_is_added_inside_its_object_and_merged_back_as_new_versions()
    {
        var (room, chair, point) = await RoomWithObjectAsync();
        var imaging = new SkiaRoomImaging();
        var adder = new AddRoomObject(_rooms, new FixedUser("user-1"), _storage, imaging);
        MarkStroke[] dab = [new(OutlineBrushMode.Paint, 2, [point])];

        var added = await adder.AddAsync(room.Id, 0, "seat", ObjectCategory.Other, dab, chair.Index, CancellationToken.None);

        Assert.True(added!.Succeeded, added.Error);
        var seat = room.Objects[^1];

        Assert.Equal((chair.Index, chair.Category, "seat"), (seat.ParentIndex, seat.Category, seat.Label));
        var labels = DecodeLabels(_storage.Files[room.LabelMapKey!].Content);

        Assert.Equal(seat.Index, LabelAt(labels, point));

        // A part has no parts, and is merged back rather than removed on its own.
        var ofPart = await adder.AddAsync(room.Id, 1, "stitching", ObjectCategory.Other, dab, seat.Index, CancellationToken.None);

        Assert.Equal(DesignApp.Application.Constants.Messages.PartParent, ofPart!.Error);
        var removed = await Remover().RemoveAsync(room.Id, 1, [seat.Index], CancellationToken.None);

        Assert.Equal(DesignApp.Application.Constants.Messages.PartRemoval("seat", chair.Label), removed!.Error);

        var merger = new MergeRoomPart(_rooms, new FixedUser("user-1"), _storage, imaging);

        Assert.Equal(DesignApp.Application.Constants.Messages.NotAPart, (await merger.MergeAsync(room.Id, 1, chair.Index, CancellationToken.None))!.Error);
        var merged = await merger.MergeAsync(room.Id, 1, seat.Index, CancellationToken.None);

        Assert.True(merged!.Succeeded, merged.Error);
        Assert.Equal((2, 2), (room.LabelMapRevision, room.LatestLabelMapRevision));
        Assert.Equal(chair.Index, LabelAt(DecodeLabels(_storage.Files[room.LabelMapKey!].Content), point));
        Assert.Contains(room.Objects, o => o.Index == seat.Index); // still listed, so undo brings it back
        Assert.Null(await new MergeRoomPart(_rooms, new FixedUser("someone-else"), _storage, imaging).MergeAsync(room.Id, 2, seat.Index, CancellationToken.None));
    }

    [Fact]
    public async Task The_ai_finds_an_objects_parts_inside_it_as_a_new_version()
    {
        var (room, sofa, _) = await RoomWithObjectAsync();
        var before = DecodeLabels(_storage.Files[room.LabelMapKey!].Content);
        var next = room.NextObjectIndex;

        var found = await Finder().FindAsync(room.Id, 0, sofa.Index, null, CancellationToken.None);

        Assert.True(found!.Succeeded, found.Error);
        var parts = room.Objects.Where(o => o.ParentIndex == sofa.Index).ToList();

        Assert.NotEmpty(parts);
        Assert.Equal(Enumerable.Range(next, parts.Count), parts.Select(p => p.Index));
        Assert.All(parts, p => Assert.Equal(sofa.Category, p.Category));
        Assert.Equal((1, 1), (room.LabelMapRevision, room.LatestLabelMapRevision));
        // The parts only took the sofa's pixels; everything else is as it was.
        var after = DecodeLabels(_storage.Files[room.LabelMapKey!].Content);
        var partIndexes = parts.Select(p => (byte)p.Index).ToHashSet();

        Assert.All(Enumerable.Range(0, after.Length).Where(p => after[p] != before[p]), p =>
        {
            Assert.Equal(sofa.Index, before[p]);
            Assert.Contains(after[p], partIndexes);
        });

        // Parts have no parts of their own.
        var ofPart = await Finder().FindAsync(room.Id, 1, parts[0].Index, null, CancellationToken.None);

        Assert.Equal(DesignApp.Application.Constants.Messages.PartsOfPart(sofa.Label), ofPart!.Error);
        Assert.Null(await new FindRoomParts(_rooms, new FixedUser("someone-else"), _storage, new SkiaRoomImaging(), Ai()).FindAsync(room.Id, 1, sofa.Index, null, CancellationToken.None));
    }

    [Fact]
    public void A_smaller_part_sits_on_top_of_a_larger_one()
    {
        // Object 1 covers the whole 20x10 map; the AI's "frame" covers all of it and its "seat" the left half.
        using var bitmap = new SKBitmap(new SKImageInfo(W, H, SKColorType.Gray8, SKAlphaType.Opaque));

        bitmap.GetPixelSpan().Fill(1);
        using var png = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        var chair = new DetectedObject(1, "chair", ObjectCategory.Furniture, new BoundingBox(0, 0, 1, 1), HexColor.Parse("#202020"));

        SegmentedObject Part(string label, Func<int, bool> covers)
        {
            var coverage = Enumerable.Range(0, W * H).Select(p => covers(p % W) ? (byte)255 : (byte)0).ToArray();

            return new(new InventoryItem(label, ObjectCategory.Furniture, new BoundingBox(0, 0, 1, 1), HexColor.Parse("#202020")), new ObjectMask(W, H, coverage));
        }

        using var photo = new MemoryStream(TestImages.Create(W, H));
        var added = new SkiaRoomImaging().AddParts(png.ToArray(), photo, chair, 2, [Part("frame", _ => true), Part("seat", x => x < W / 2)]);

        Assert.True(added.Succeeded, added.Error);
        Assert.Equal(["frame", "seat"], added.Value.Objects.Select(o => o.Label));
        var labels = SkiaRoomImaging.DecodeGrayPng(added.Value.Png).Labels;

        Assert.Equal([3, 2], new[] { labels[5 * W + 2], labels[5 * W + 15] }.Select(l => (int)l));
    }

    [Fact]
    public async Task Instructions_for_the_ai_reach_it_and_are_limited_in_length()
    {
        var (room, sofa, _) = await RoomWithObjectAsync();

        var tooLong = await Finder().FindAsync(room.Id, 0, sofa.Index, new string('x', DesignApp.Application.Constants.Parts.MaxInstructionsLength + 1), CancellationToken.None);

        Assert.Equal(DesignApp.Application.Constants.Messages.PartsInstructions, tooLong!.Error);
        Assert.Equal(0, room.LatestLabelMapRevision);
        Assert.EndsWith("\"the cushions on their own\"", DesignApp.Infrastructure.Constants.Ai.Claude.PartsMessage("sofa", 1, 2, 3, 4, "the cushions on their own"), StringComparison.Ordinal);
        Assert.DoesNotContain("asks", DesignApp.Infrastructure.Constants.Ai.Claude.PartsMessage("sofa", 1, 2, 3, 4, null), StringComparison.Ordinal);
        Assert.True((await Finder().FindAsync(room.Id, 0, sofa.Index, "  ", CancellationToken.None))!.Succeeded);
    }

    [Fact]
    public void A_room_adds_found_parts_with_its_next_indexes_in_one_version()
    {
        var chair = new DetectedObject(1, "chair", ObjectCategory.Furniture, Box, HexColor.Parse("#806040"));
        var room = ReadyRoom([chair]);

        DetectedObject Part(int index, int parent)
        {
            return new(index, "seat", ObjectCategory.Furniture, Box, HexColor.Parse("#A08060"), ParentIndex: parent);
        }

        Assert.Throws<DomainException>(() => room.AddParts([Part(3, 1)], 0, r => $"labels/{r}"));
        Assert.Throws<DomainException>(() => room.AddParts([Part(2, 9)], 0, r => $"labels/{r}"));
        Assert.Equal(1, room.AddParts([Part(2, 1), Part(3, 1)], 0, r => $"labels/{r}"));
        Assert.Equal(4, room.NextObjectIndex);
    }

    [Fact]
    public async Task Removing_an_object_removes_its_parts_too()
    {
        var (room, chair, point) = await RoomWithObjectAsync();
        var added = await new AddRoomObject(_rooms, new FixedUser("user-1"), _storage, new SkiaRoomImaging())
            .AddAsync(room.Id, 0, "seat", ObjectCategory.Other, [new(OutlineBrushMode.Paint, 2, [point])], chair.Index, CancellationToken.None);

        Assert.True(added!.Succeeded, added.Error);
        var seat = room.Objects[^1];

        var removed = await Remover().RemoveAsync(room.Id, 1, [chair.Index], CancellationToken.None);

        Assert.True(removed!.Succeeded, removed.Error);
        var labels = DecodeLabels(_storage.Files[room.LabelMapKey!].Content);

        Assert.DoesNotContain((byte)chair.Index, labels);
        Assert.DoesNotContain((byte)seat.Index, labels);
    }

    /// <summary>A ready room with the given objects.</summary>
    private static Room ReadyRoom(IEnumerable<DetectedObject> objects)
    {
        var room = Room.Upload(Guid.CreateVersion7(Now), "user-1", "photo", 600, 400, Now);

        room.StartAnalysis();
        room.StartSegmentation();
        room.MarkReady(objects, "labels/0");

        return room;
    }

    /// <summary>An analyzed room, one of its objects that can be removed, and a point well inside it.</summary>
    private async Task<(Room Room, DetectedObject Object, StrokePoint Point)> RoomWithObjectAsync()
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

        // The first removable object with a pixel whose neighbours are all its own, so a small dab stays inside it.
        var labels = DecodeLabels(_storage.Files[DesignApp.Application.Constants.Rooms.LabelMapKey(room.Id, 0)].Content);

        foreach (var candidate in room.Objects.Where(o => !o.Category.IsSurface()))
        {
            for (var p = 0; p < labels.Length; p++)
            {
                var (x, y) = (p % 600, p / 600);

                if (x < 3 || y < 3 || x > 596 || y > 396 || labels[p] != candidate.Index)
                {
                    continue;
                }

                if (Enumerable.Range(-3, 7).All(d => labels[p + d] == candidate.Index && labels[p + d * 600] == candidate.Index))
                {
                    return (room, candidate, new StrokePoint((x + 0.5) / 600, (y + 0.5) / 400));
                }
            }
        }

        throw new InvalidOperationException("The fake analysis found no object to use.");
    }

    /// <summary>The fake AI.</summary>
    private static FixedAiProviders Ai()
    {
        return new FixedAiProviders(new FakeInventoryProvider(NoDelay), new FakeSegmentationProvider(NoDelay), new FakeObjectRemovalProvider(NoDelay));
    }

    /// <summary>The removal use case with the fake AI.</summary>
    private RemoveRoomObject Remover()
    {
        return new RemoveRoomObject(_rooms, new FixedUser("user-1"), _storage, new SkiaRoomImaging(), Ai());
    }

    /// <summary>The part-finding use case with the fake AI.</summary>
    private FindRoomParts Finder()
    {
        return new FindRoomParts(_rooms, new FixedUser("user-1"), _storage, new SkiaRoomImaging(), Ai());
    }

    /// <summary>The label at a point of a 600x400 label map.</summary>
    private static int LabelAt(byte[] labels, StrokePoint point)
    {
        return labels[(int)(point.Y * 400) * 600 + (int)(point.X * 600)];
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
