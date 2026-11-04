using DesignApp.Application.Analysis;
using DesignApp.Application.Common;
using DesignApp.Application.Rooms;
using DesignApp.Domain.Rooms;
using DesignApp.Infrastructure.Ai;
using DesignApp.Infrastructure.Ai.Fakes;
using DesignApp.Infrastructure.Photos;
using DesignApp.Tests;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DesignApp.UnitTests.Analysis;

/// <summary>
/// The analysis use case end to end in memory: real label-map building on a real photo, with the
/// fake AI providers (or stubs) in place of Claude and SAM 3.
/// </summary>
public class AnalyzeRoomTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);
    private static readonly IOptions<AiOptions> NoDelay = Options.Create(new AiOptions { UseFakes = true });

    private readonly FakeRooms _rooms = new();
    private readonly FakeStorage _storage = new();
    private readonly RecordingNotifier _notifier = new();

    private IObjectInventoryProvider _inventory = new FakeInventoryProvider(NoDelay);
    private ISegmentationProvider _segmentation = new FakeSegmentationProvider(NoDelay);

    /// <summary>The use case wired to this test's fakes.</summary>
    private AnalyzeRoom CreateHandler() =>
        new(_rooms, _storage, new SkiaRoomImaging(), _inventory, _segmentation, _notifier, NullLogger<AnalyzeRoom>.Instance);

    /// <summary>Saves a freshly uploaded room with a real photo in storage.</summary>
    private async Task<Room> UploadedRoomAsync()
    {
        var id = Guid.CreateVersion7(Now);
        var room = Room.Upload(id, "user-1", StorageKeys.RoomPhoto(id), 1200, 800, Now);
        _storage.Files[room.PhotoKey] = (TestImages.Create(1200, 800), "image/jpeg");
        _rooms.Add(room);
        await _rooms.SaveChangesAsync(CancellationToken.None);
        return room;
    }

    [Fact]
    public async Task A_room_moves_through_each_stage_to_ready()
    {
        var room = await UploadedRoomAsync();

        await CreateHandler().HandleAsync(room.Id, CancellationToken.None);

        Assert.Equal(RoomStatus.Ready, room.Status);
        Assert.Equal([RoomStatus.Analyzing, RoomStatus.Segmenting, RoomStatus.Ready], _notifier.Sent);
        Assert.Equal(StorageKeys.RoomLabelMap(room.Id), room.LabelMapKey);
        Assert.Equal("image/png", _storage.Files[room.LabelMapKey!].ContentType);
        Assert.Equal(FakeInventoryProvider.Layout.Select(i => i.Label), room.Objects.Select(o => o.Label));
        Assert.Equal(Enumerable.Range(1, room.Objects.Count), room.Objects.Select(o => o.Index));
    }

    [Fact]
    public async Task A_photo_that_is_not_a_room_fails_with_the_reason()
    {
        var room = await UploadedRoomAsync();
        _inventory = new StubInventory(Result.Failure<IReadOnlyList<InventoryItem>>("Not a room."));

        await CreateHandler().HandleAsync(room.Id, CancellationToken.None);

        Assert.Equal(RoomStatus.Failed, room.Status);
        Assert.Equal("Not a room.", room.FailureReason);
        Assert.Equal([RoomStatus.Analyzing, RoomStatus.Failed], _notifier.Sent);
    }

    [Fact]
    public async Task A_provider_error_fails_the_room_without_exposing_details()
    {
        var room = await UploadedRoomAsync();
        _segmentation = new ThrowingSegmentation();

        await CreateHandler().HandleAsync(room.Id, CancellationToken.None);

        Assert.Equal(RoomStatus.Failed, room.Status);
        Assert.Equal(AnalyzeRoom.UnexpectedFailure, room.FailureReason);
        Assert.Equal(RoomStatus.Failed, _notifier.Sent[^1]);
    }

    [Fact]
    public async Task No_outlines_means_nothing_found()
    {
        var room = await UploadedRoomAsync();
        _segmentation = new EmptySegmentation();

        await CreateHandler().HandleAsync(room.Id, CancellationToken.None);

        Assert.Equal(RoomStatus.Failed, room.Status);
        Assert.Equal(AnalyzeRoom.NothingFound, room.FailureReason);
        Assert.Null(room.LabelMapKey);
    }

    [Fact]
    public async Task An_interrupted_analysis_runs_again()
    {
        var room = await UploadedRoomAsync();
        room.StartAnalysis(); // as if the API stopped half-way

        await CreateHandler().HandleAsync(room.Id, CancellationToken.None);

        Assert.Equal(RoomStatus.Ready, room.Status);
        Assert.Equal(2, room.AnalysisAttempts);
    }

    [Fact]
    public async Task A_room_that_keeps_getting_interrupted_is_failed()
    {
        var room = await UploadedRoomAsync();
        for (var i = 0; i < Room.MaxAnalysisAttempts; i++)
        {
            room.StartAnalysis();
            room.RestartAnalysis();
        }

        await CreateHandler().HandleAsync(room.Id, CancellationToken.None);

        Assert.Equal(RoomStatus.Failed, room.Status);
        Assert.Equal(AnalyzeRoom.UnexpectedFailure, room.FailureReason);
    }

    [Fact]
    public async Task A_finished_room_is_left_alone()
    {
        var room = await UploadedRoomAsync();
        await CreateHandler().HandleAsync(room.Id, CancellationToken.None);
        _notifier.Sent.Clear();

        await CreateHandler().HandleAsync(room.Id, CancellationToken.None);

        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task Shutting_down_leaves_the_room_to_resume_later()
    {
        var room = await UploadedRoomAsync();
        using var shutdown = new CancellationTokenSource();
        _inventory = new CancellingInventory(shutdown);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateHandler().HandleAsync(room.Id, shutdown.Token));

        Assert.Equal(RoomStatus.Analyzing, room.Status);
    }

    /// <summary>Returns a fixed inventory result.</summary>
    /// <param name="result">What to return.</param>
    private sealed class StubInventory(Result<IReadOnlyList<InventoryItem>> result) : IObjectInventoryProvider
    {
        public Task<Result<IReadOnlyList<InventoryItem>>> ListObjectsAsync(AnalysisImage image, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    /// <summary>Cancels the analysis as if the API were shutting down.</summary>
    /// <param name="shutdown">The token source to cancel.</param>
    private sealed class CancellingInventory(CancellationTokenSource shutdown) : IObjectInventoryProvider
    {
        public async Task<Result<IReadOnlyList<InventoryItem>>> ListObjectsAsync(AnalysisImage image, CancellationToken cancellationToken)
        {
            await shutdown.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("unreachable");
        }
    }

    /// <summary>Fails like a provider outage.</summary>
    private sealed class ThrowingSegmentation : ISegmentationProvider
    {
        public Task<IReadOnlyList<ObjectMask?>> SegmentAsync(AnalysisImage image, IReadOnlyList<InventoryItem> items, CancellationToken cancellationToken) =>
            throw new HttpRequestException("fal.ai is down");
    }

    /// <summary>Finds nothing for any item.</summary>
    private sealed class EmptySegmentation : ISegmentationProvider
    {
        public Task<IReadOnlyList<ObjectMask?>> SegmentAsync(AnalysisImage image, IReadOnlyList<InventoryItem> items, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ObjectMask?>>([.. items.Select(_ => (ObjectMask?)null)]);
    }
}
