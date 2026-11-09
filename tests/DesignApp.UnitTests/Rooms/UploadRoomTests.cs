using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Application.UseCases.Room;


namespace DesignApp.UnitTests.Rooms;

/// <summary>The upload use case, with fakes in place of the database, storage and photo processor.</summary>
public class UploadRoomTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    private readonly FixedPhotoProcessor _photos = new();
    private readonly FakeStorage _storage = new();
    private readonly FakeRooms _rooms = new();
    private readonly RecordingScheduler _scheduler = new();

    /// <summary>The use case wired to this test's fakes.</summary>
    private UploadRoom CreateHandler() =>
        new(_photos, _storage, _rooms, new FixedUser("user-1"), new FixedClock(Now), _scheduler);

    [Fact]
    public async Task Stores_the_photo_under_the_room_and_saves_the_room()
    {
        var result = await CreateHandler().HandleAsync(new MemoryStream([1, 2, 3]), CancellationToken.None);

        var room = result.Value;
        Assert.Equal(DesignApp.Application.Constants.Rooms.PhotoKey(room.Id), room.PhotoKey);
        Assert.Equal(DesignApp.Application.Constants.ContentTypes.Jpeg, _storage.Files[room.PhotoKey].ContentType);
        Assert.Equal((1600, 1200), (room.Width, room.Height));
        Assert.Equal("user-1", room.OwnerId);
        Assert.Equal(Now, room.CreatedAt);
        Assert.Same(room, Assert.Single(_rooms.Saved));
    }

    [Fact]
    public async Task Schedules_the_analysis_once_the_room_is_saved()
    {
        var result = await CreateHandler().HandleAsync(new MemoryStream([1, 2, 3]), CancellationToken.None);

        Assert.Equal(result.Value.Id, Assert.Single(_scheduler.Scheduled));
    }

    [Fact]
    public async Task A_refused_photo_stores_nothing()
    {
        _photos.Refusal = "This photo is too small.";

        var result = await CreateHandler().HandleAsync(new MemoryStream([1, 2, 3]), CancellationToken.None);

        Assert.Equal("This photo is too small.", result.Error);
        Assert.Empty(_storage.Files);
        Assert.Empty(_rooms.Saved);
        Assert.Empty(_scheduler.Scheduled);
    }

    [Fact]
    public async Task Removes_the_stored_photo_when_the_room_cannot_be_saved()
    {
        _rooms.FailOnSave = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateHandler().HandleAsync(new MemoryStream([1, 2, 3]), CancellationToken.None));

        Assert.Empty(_storage.Files);
        Assert.Empty(_scheduler.Scheduled);
    }

    /// <summary>Accepts every file as a 1600x1200 photo, unless <see cref="Refusal"/> is set.</summary>
    private sealed class FixedPhotoProcessor : IPhotoProcessor
    {
        /// <summary>When set, every file is refused with this message.</summary>
        public string? Refusal { get; set; }

        public Result<ProcessedPhoto> Process(Stream upload) =>
            Refusal is null ? new ProcessedPhoto([0xFF, 0xD8], 1600, 1200) : Result.Failure<ProcessedPhoto>(Refusal);
    }
}
