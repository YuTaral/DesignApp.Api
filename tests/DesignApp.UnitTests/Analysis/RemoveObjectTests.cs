using DesignApp.Application.Objects;
using DesignApp.Application.UseCases;
using DesignApp.Domain.Entities;
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
/// Removing an object from the photo: the label map refill on its own, the free fake eraser, the room's photo
/// versions, and the use case with stored files.
/// </summary>
public class RemoveObjectTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);
    private static readonly IOptions<AiOptions> NoDelay = Options.Create(new AiOptions { UseFakes = true });

    private readonly FakeRooms _rooms = new();
    private readonly FakeStorage _storage = new();

    [Fact]
    public void The_removed_pixels_go_to_the_objects_around_them_and_old_gaps_stay()
    {
        // 10x10: a wall (1) on the left half, a floor (2) on the right half, a lamp (3) in the middle, one old gap.
        const int W = 10, H = 10;
        var labels = new byte[W * H];

        for (var p = 0; p < labels.Length; p++)
        {
            labels[p] = (byte)(p % W < 5 ? 1 : 2);
        }

        for (var y = 3; y < 7; y++)
        {
            for (var x = 3; x < 7; x++)
            {
                labels[y * W + x] = 3;
            }
        }

        labels[0] = 0;
        var rgba = Enumerable.Repeat((byte)128, W * H * 4).ToArray();

        var removed = RemovalRefill.Apply(labels, W, H, rgba, [3]);

        Assert.Equal(16, removed);
        Assert.DoesNotContain((byte)3, labels);
        Assert.Equal(0, labels[0]);
        Assert.All(labels.Skip(1), l => Assert.True(l is 1 or 2));
    }

    [Fact]
    public void The_fake_eraser_fills_the_mask_from_its_surroundings()
    {
        using var bitmap = new SKBitmap(40, 40);

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(120, 120, 120));
            canvas.DrawRect(15, 15, 10, 10, new SKPaint { Color = SKColors.Red });
        }

        var photo = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Jpeg, 95).ToArray();
        using var maskBitmap = new SKBitmap(new SKImageInfo(40, 40, SKColorType.Gray8, SKAlphaType.Opaque));

        using (var canvas = new SKCanvas(maskBitmap))
        {
            canvas.Clear(SKColors.Black);
            canvas.DrawRect(13, 13, 14, 14, new SKPaint { Color = SKColors.White });
        }

        var mask = SKImage.FromBitmap(maskBitmap).Encode(SKEncodedImageFormat.Png, 100).ToArray();

        var filled = SKBitmap.Decode(FakeObjectRemovalProvider.Fill(photo, mask));

        var center = filled.GetPixel(20, 20);

        Assert.InRange(center.Red - center.Green, -20, 20); // the red square is gone, gray instead
        Assert.InRange((int)center.Green, 100, 140);
    }

    [Fact]
    public async Task A_removal_is_a_new_version_with_its_own_photo_and_undo_goes_back()
    {
        var room = await AnalyzedRoomAsync();
        var remover = Remover("user-1");
        var lamp = room.Objects.Single(o => o.Label == "floor lamp");
        var before = DecodeLabels(_storage.Files[DesignApp.Application.Constants.Rooms.LabelMapKey(room.Id, 0)].Content);

        var result = await remover.RemoveAsync(room.Id, 0, [lamp.Index], CancellationToken.None);

        Assert.True(result!.Succeeded, result.Error);
        Assert.Equal((1, 1, 1), (room.LabelMapRevision, room.PhotoVersion, room.LatestPhotoVersion));
        Assert.Equal([0, 1], room.PhotoVersions);
        Assert.Contains(lamp, room.Objects); // still listed, so undo can bring it back
        Assert.Contains((byte)lamp.Index, before);
        Assert.DoesNotContain((byte)lamp.Index, DecodeLabels(_storage.Files[room.LabelMapKey!].Content));
        var newPhoto = _storage.Files[DesignApp.Application.Constants.Rooms.PhotoKey(room.Id, 1)].Content;

        Assert.NotEqual(_storage.Files[room.PhotoKey].Content, newPhoto);

        using (var decoded = SKBitmap.Decode(newPhoto))
            Assert.Equal((room.Width, room.Height), (decoded.Width, decoded.Height));

        // Undo shows the photo with the lamp; an outline fix there keeps that photo.
        var fixer = new FixRoomOutline(_rooms, new FixedUser("user-1"), _storage, new SkiaRoomImaging());

        Assert.True((await fixer.UseRevisionAsync(room.Id, 0, CancellationToken.None))!.Succeeded);
        Assert.Equal(0, room.PhotoVersion);
        var sofa = room.Objects.Single(o => o.Label == "sofa").Index;
        var spot = Array.FindIndex(before, l => l != sofa);
        StrokePoint[] dab = [new((spot % 600 + 0.5) / 600, (spot / 600 + 0.5) / 400)];

        Assert.True((await fixer.FixAsync(room.Id, 0, sofa, OutlineBrushMode.Paint, 2, dab, StrokeShape.Brush, CancellationToken.None))!.Succeeded);
        Assert.Equal((2, 0), (room.LabelMapRevision, room.PhotoVersion));
        Assert.Equal([0, 1, 0], room.PhotoVersions);

        // A fix made on the version without the lamp keeps that version's photo.
        Assert.True((await fixer.UseRevisionAsync(room.Id, 1, CancellationToken.None))!.Succeeded);
        Assert.True((await fixer.FixAsync(room.Id, 1, sofa, OutlineBrushMode.Paint, 2, dab, StrokeShape.Brush, CancellationToken.None))!.Succeeded);
        Assert.Equal((3, 1), (room.LabelMapRevision, room.PhotoVersion));
    }

    [Fact]
    public async Task Surfaces_objects_not_in_the_photo_unknown_versions_and_other_users_are_refused()
    {
        var room = await AnalyzedRoomAsync();
        var remover = Remover("user-1");
        var wall = room.Objects.First(o => o.Category is ObjectCategory.Wall);
        var lamp = room.Objects.Single(o => o.Label == "floor lamp");

        var surface = await remover.RemoveAsync(room.Id, 0, [wall.Index], CancellationToken.None);

        Assert.Contains("can't be removed", surface!.Error, StringComparison.Ordinal);
        Assert.False((await remover.RemoveAsync(room.Id, 5, [lamp.Index], CancellationToken.None))!.Succeeded);
        Assert.False((await remover.RemoveAsync(room.Id, 0, [99], CancellationToken.None))!.Succeeded);
        Assert.Null(await Remover("someone-else").RemoveAsync(room.Id, 0, [lamp.Index], CancellationToken.None));

        Assert.True((await remover.RemoveAsync(room.Id, 0, [lamp.Index], CancellationToken.None))!.Succeeded);
        var again = await remover.RemoveAsync(room.Id, 1, [lamp.Index], CancellationToken.None);

        Assert.Equal(DesignApp.Application.Constants.Messages.ObjectNotInPhoto, again!.Error);
        Assert.Equal(1, room.LatestLabelMapRevision);
    }

    [Fact]
    public async Task Several_objects_are_removed_together_in_one_version()
    {
        var room = await AnalyzedRoomAsync();
        var remover = Remover("user-1");
        var sofa = room.Objects.Single(o => o.Label == "sofa").Index;
        var lamp = room.Objects.Single(o => o.Label == "floor lamp").Index;

        var result = await remover.RemoveAsync(room.Id, 0, [sofa, lamp, lamp], CancellationToken.None);

        Assert.True(result!.Succeeded, result.Error);
        Assert.Equal((1, 1), (room.LatestLabelMapRevision, room.LatestPhotoVersion)); // one version, one eraser call
        var labels = DecodeLabels(_storage.Files[room.LabelMapKey!].Content);

        Assert.DoesNotContain((byte)sofa, labels);
        Assert.DoesNotContain((byte)lamp, labels);

        Assert.Equal(DesignApp.Application.Constants.Messages.RemovalCount, (await remover.RemoveAsync(room.Id, 1, [], CancellationToken.None))!.Error);
        Assert.Equal(DesignApp.Application.Constants.Messages.RemovalCount,
            (await remover.RemoveAsync(room.Id, 1, [.. Enumerable.Range(1, 21)], CancellationToken.None))!.Error);
    }

    /// <summary>The use case with the fake eraser, for one user.</summary>
    /// <param name="user">Who is asking.</param>
    private RemoveRoomObject Remover(string user)
    {
        var ai = new FixedAiProviders(new FakeInventoryProvider(NoDelay), new FakeSegmentationProvider(NoDelay), new FakeObjectRemovalProvider(NoDelay));

        return new RemoveRoomObject(_rooms, new FixedUser(user), _storage, new SkiaRoomImaging(), ai);
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
