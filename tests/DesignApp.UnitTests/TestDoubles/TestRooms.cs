using DesignApp.Application.UseCases;
using DesignApp.Domain.Entities;
using DesignApp.Infrastructure.Ai;
using DesignApp.Infrastructure.Ai.Fakes;
using DesignApp.Infrastructure.Photos;
using DesignApp.Tests;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;

using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests;

/// <summary>The rooms and label maps the outline tests start from.</summary>
internal static class TestRooms
{
    /// <summary>The width of <see cref="SmallLabels"/>.</summary>
    public const int SmallWidth = 20;

    /// <summary>The height of <see cref="SmallLabels"/>.</summary>
    public const int SmallHeight = 10;

    private static readonly IOptions<AiOptions> NoDelay = Options.Create(new AiOptions { UseFakes = true });

    /// <summary>A 20x10 label map: object 1 covers x &lt; 6, object 2 covers x &gt;= 10, x 6..9 is unassigned.</summary>
    public static byte[] SmallLabels()
    {
        var labels = new byte[SmallWidth * SmallHeight];

        for (var p = 0; p < labels.Length; p++)
        {
            labels[p] = (byte)(p % SmallWidth < 6 ? 1 : p % SmallWidth >= 10 ? 2 : 0);
        }

        return labels;
    }

    /// <summary>Uploads a 600x400 room for <c>user-1</c> with a real photo, and analyzes it with the fake AI.</summary>
    /// <param name="rooms">The room database.</param>
    /// <param name="storage">The file storage.</param>
    /// <param name="now">The upload time, also the room id's time.</param>
    /// <param name="scope">What the analysis looks for.</param>
    /// <returns>The room, ready.</returns>
    public static async Task<Room> AnalyzedAsync(FakeRooms rooms, FakeStorage storage, DateTimeOffset now, AnalysisScope scope = AnalysisScope.WholeRoom)
    {
        var id = Guid.CreateVersion7(now);
        var room = Room.Upload(id, "user-1", Application.Constants.Rooms.PhotoKey(id), 600, 400, now, scope);
        var analyze = new AnalyzeRoom(rooms, storage, new SkiaRoomImaging(),
            new FixedAiProviders(new FakeInventoryProvider(NoDelay), new FakeSegmentationProvider(NoDelay)),
            new RecordingNotifier(), NullLogger<AnalyzeRoom>.Instance);

        storage.Files[room.PhotoKey] = (TestImages.Create(600, 400), Application.Constants.ContentTypes.Jpeg);
        rooms.Add(room);
        await rooms.SaveChangesAsync(CancellationToken.None);
        await analyze.HandleAsync(room.Id, CancellationToken.None);
        Assert.Equal(RoomStatus.Ready, room.Status);

        return room;
    }

    /// <summary>Decodes a label map PNG to one byte per pixel.</summary>
    /// <param name="png">The PNG.</param>
    public static byte[] DecodeLabels(byte[] png)
    {
        using var codec = SKCodec.Create(new MemoryStream(png));
        using var bitmap = SKBitmap.Decode(codec, new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Gray8, SKAlphaType.Opaque));

        return bitmap.GetPixelSpan().ToArray();
    }
}
