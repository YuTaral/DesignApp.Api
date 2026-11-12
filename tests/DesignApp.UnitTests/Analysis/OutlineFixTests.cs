using DesignApp.Application.Objects;
using DesignApp.Application.UseCases;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Extensions;
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
/// Fixing outlines with a brush (the stroke on its own, and the use case with stored label map
/// versions), the analysis scope chosen at upload, and the fake/real AI choice.
/// </summary>
public class OutlineFixTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);
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

    /// <summary>A stroke on object 1 through label-map pixel centers.</summary>
    private static OutlineStroke Stroke(OutlineBrushMode mode, double radius, params (double X, double Y)[] pixels)
    {
        return new(1, "wall", mode, radius, [.. pixels.Select(p => new StrokePoint((p.X + 0.5) / W, (p.Y + 0.5) / H))]);
    }

    [Fact]
    public void A_click_paints_a_round_dab()
    {
        var labels = Labels();

        // Radius 1.5 around pixel (8, 5): pixel centers within 1.5 px are the 3x3 block (its corners
        // are 1.41 away); (8, 3), (8, 7), (6, 5) and (10, 5) are 2 away, so they stay out.
        var changed = OutlineBrush.Apply(labels, W, H, Stroke(OutlineBrushMode.Paint, 1.5, (8, 5)));

        Assert.Equal(9, changed.Value);
        Assert.Equal(1, labels[5 * W + 8]);
        Assert.Equal(1, labels[4 * W + 9]);
        Assert.Equal(0, labels[3 * W + 8]);
    }

    [Fact]
    public void A_drag_paints_a_continuous_line_between_its_points()
    {
        var labels = Labels();

        OutlineBrush.Apply(labels, W, H, Stroke(OutlineBrushMode.Paint, 0.6, (6, 0), (6, 9)));

        Assert.All(Enumerable.Range(0, H), y => Assert.Equal(1, labels[y * W + 6]));
        Assert.All(Enumerable.Range(0, H), y => Assert.Equal(0, labels[y * W + 7]));
    }

    [Fact]
    public void Painting_takes_pixels_from_other_objects()
    {
        var labels = Labels();

        OutlineBrush.Apply(labels, W, H, Stroke(OutlineBrushMode.Paint, 0.6, (12, 5)));

        Assert.Equal(1, labels[5 * W + 12]);
    }

    [Fact]
    public void Erasing_only_clears_the_selected_object()
    {
        var labels = Labels();

        // A big brush over the border between object 1 and the gap and object 2.
        var changed = OutlineBrush.Apply(labels, W, H, Stroke(OutlineBrushMode.Erase, 6, (8, 5)));

        Assert.True(changed.Value > 0);
        Assert.Equal(0, labels[5 * W + 4]);   // object 1, erased
        Assert.Equal(2, labels[5 * W + 12]);  // object 2, untouched
        Assert.Equal(1, labels[5 * W + 0]);   // object 1 outside the brush, kept
    }

    [Theory]
    [InlineData(OutlineBrushMode.Paint, 2.0, 1.0, "already the wall")]       // inside the wall
    [InlineData(OutlineBrushMode.Erase, 15.0, 1.0, "There's no wall there")] // on object 2
    [InlineData(OutlineBrushMode.Erase, 8.0, 40.0, "erase the whole wall")]  // a brush bigger than the photo
    public void Strokes_that_change_nothing_useful_are_refused(OutlineBrushMode mode, double x, double radius, string message)
    {
        var labels = Labels();

        var result = OutlineBrush.Apply(labels, W, H, Stroke(mode, radius, (x, 5)));

        Assert.False(result.Succeeded);
        Assert.Contains(message, result.Error, StringComparison.Ordinal);
        Assert.Equal(Labels(), labels);
    }

    [Fact]
    public async Task A_fix_is_stored_as_a_new_version_and_undo_can_go_back()
    {
        var room = await AnalyzedRoomAsync(AnalysisScope.WholeRoom);
        var imaging = new SkiaRoomImaging();
        var fixer = new FixRoomOutline(_rooms, new FixedUser("user-1"), _storage, imaging);
        var before = DecodeLabels(_storage.Files[DesignApp.Application.Constants.Rooms.LabelMapKey(room.Id)].Content);

        // Paint a dab on a pixel the sofa doesn't have yet.
        var image = imaging.PrepareForAnalysis(new MemoryStream(_storage.Files[room.PhotoKey].Content));
        var sofa = room.Objects.Single(o => o.Label == "sofa").Index;
        var spot = Array.FindIndex(before, l => l != sofa);
        StrokePoint[] dab = [new((spot % image.Width + 0.5) / image.Width, (spot / image.Width + 0.5) / image.Height)];

        var result = await fixer.FixAsync(room.Id, 0, sofa, OutlineBrushMode.Paint, 2, dab, StrokeShape.Brush, CancellationToken.None);

        Assert.True(result!.Succeeded, result.Error);
        Assert.Equal((1, 1), (room.LabelMapRevision, room.LatestLabelMapRevision));
        Assert.Equal(DesignApp.Application.Constants.Rooms.LabelMapKey(room.Id, 1), room.LabelMapKey);
        Assert.Equal(sofa, DecodeLabels(_storage.Files[room.LabelMapKey!].Content)[spot]);
        Assert.Equal(before, DecodeLabels(_storage.Files[DesignApp.Application.Constants.Rooms.LabelMapKey(room.Id, 0)].Content)); // version 0 is kept

        Assert.True((await fixer.UseRevisionAsync(room.Id, 0, CancellationToken.None))!.Succeeded);
        Assert.Equal((0, 1), (room.LabelMapRevision, room.LatestLabelMapRevision));

        Assert.False((await fixer.UseRevisionAsync(room.Id, 2, CancellationToken.None))!.Succeeded);
        Assert.Null(await new FixRoomOutline(_rooms, new FixedUser("someone-else"), _storage, imaging)
            .FixAsync(room.Id, 0, sofa, OutlineBrushMode.Paint, 2, dab, StrokeShape.Brush, CancellationToken.None));
        Assert.False((await fixer.FixAsync(room.Id, 1, sofa, OutlineBrushMode.Paint, 9999, dab, StrokeShape.Brush, CancellationToken.None))!.Succeeded);
        Assert.False((await fixer.FixAsync(room.Id, 1, sofa, OutlineBrushMode.Paint, 2, [], StrokeShape.Brush, CancellationToken.None))!.Succeeded);
    }

    [Fact]
    public async Task Objects_only_skips_walls_floors_ceilings_doors_and_windows()
    {
        var room = await AnalyzedRoomAsync(AnalysisScope.ObjectsOnly);

        Assert.Equal(["sofa", "rug", "floor lamp", "potted plant"], room.Objects.Select(o => o.Label));
    }

    [Fact]
    public async Task A_room_is_analyzed_by_the_ai_it_was_uploaded_with()
    {
        var id = Guid.CreateVersion7(Now);
        var room = Room.Upload(id, "user-1", DesignApp.Application.Constants.Rooms.PhotoKey(id), 600, 400, Now, aiMode: AiMode.Real);
        _storage.Files[room.PhotoKey] = (TestImages.Create(600, 400), DesignApp.Application.Constants.ContentTypes.Jpeg);
        _rooms.Add(room);
        await _rooms.SaveChangesAsync(CancellationToken.None);
        var ai = new FixedAiProviders(new FakeInventoryProvider(NoDelay), new FakeSegmentationProvider(NoDelay));

        await new AnalyzeRoom(_rooms, _storage, new SkiaRoomImaging(), ai, new RecordingNotifier(), NullLogger<AnalyzeRoom>.Instance)
            .HandleAsync(room.Id, CancellationToken.None);

        Assert.Equal([AiMode.Real], ai.Asked);
    }

    [Theory]
    [InlineData(AiMode.Fake, false, false, AiMode.Fake, true)]
    [InlineData(AiMode.Fake, false, true, AiMode.Real, false)]   // no switch: only the default
    [InlineData(AiMode.Fake, true, false, AiMode.Real, false)]   // switch, but no keys
    [InlineData(AiMode.Fake, true, true, AiMode.Real, true)]
    [InlineData(AiMode.Real, false, true, AiMode.Fake, false)]   // production: never the fakes
    public void Uploads_may_use_the_default_ai_or_choose_where_the_switch_is_on(
        AiMode defaultMode, bool canChoose, bool realAvailable, AiMode asked, bool allowed)
    {
        Assert.Equal(allowed, new AiAvailability(defaultMode, canChoose, realAvailable).Allows(asked));
    }

    [Theory]
    [InlineData(AnalysisScope.WholeRoom, ObjectCategory.Opening, true)]
    [InlineData(AnalysisScope.NoDoorsOrWindows, ObjectCategory.Opening, false)]
    [InlineData(AnalysisScope.NoDoorsOrWindows, ObjectCategory.Wall, true)]
    [InlineData(AnalysisScope.ObjectsOnly, ObjectCategory.Ceiling, false)]
    [InlineData(AnalysisScope.ObjectsOnly, ObjectCategory.Fixture, true)]
    public void Scopes_include_the_right_categories(AnalysisScope scope, ObjectCategory category, bool included)
    {
        Assert.Equal(included, scope.Includes(category));
    }

    /// <summary>Uploads and analyzes a room with the fake AI and a real photo.</summary>
    /// <param name="scope">What the analysis looks for.</param>
    private async Task<Room> AnalyzedRoomAsync(AnalysisScope scope)
    {
        var id = Guid.CreateVersion7(Now);
        var room = Room.Upload(id, "user-1", DesignApp.Application.Constants.Rooms.PhotoKey(id), 600, 400, Now, scope);
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
