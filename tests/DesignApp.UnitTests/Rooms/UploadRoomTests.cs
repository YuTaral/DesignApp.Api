using DesignApp.Application.Abstractions;
using DesignApp.Application.Common;
using DesignApp.Application.Photos;
using DesignApp.Application.Rooms;
using DesignApp.Domain.Rooms;

namespace DesignApp.UnitTests.Rooms;

public class UploadRoomTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    private readonly FixedPhotoProcessor _photos = new();
    private readonly FakeStorage _storage = new();
    private readonly FakeRooms _rooms = new();

    private UploadRoom CreateHandler() =>
        new(_photos, _storage, _rooms, new FixedUser("user-1"), new FixedClock(Now));

    [Fact]
    public async Task Stores_the_photo_under_the_room_and_saves_the_room()
    {
        var result = await CreateHandler().HandleAsync(new MemoryStream([1, 2, 3]), CancellationToken.None);

        var room = result.Value;
        Assert.Equal(StorageKeys.RoomPhoto(room.Id), room.PhotoKey);
        Assert.Equal(ProcessedPhoto.ContentType, _storage.Files[room.PhotoKey].ContentType);
        Assert.Equal((1600, 1200), (room.Width, room.Height));
        Assert.Equal("user-1", room.OwnerId);
        Assert.Equal(Now, room.CreatedAt);
        Assert.Same(room, Assert.Single(_rooms.Saved));
    }

    [Fact]
    public async Task A_refused_photo_stores_nothing()
    {
        _photos.Refusal = "This photo is too small.";

        var result = await CreateHandler().HandleAsync(new MemoryStream([1, 2, 3]), CancellationToken.None);

        Assert.Equal("This photo is too small.", result.Error);
        Assert.Empty(_storage.Files);
        Assert.Empty(_rooms.Saved);
    }

    [Fact]
    public async Task Removes_the_stored_photo_when_the_room_cannot_be_saved()
    {
        _rooms.FailOnSave = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateHandler().HandleAsync(new MemoryStream([1, 2, 3]), CancellationToken.None));

        Assert.Empty(_storage.Files);
    }

    private sealed class FixedPhotoProcessor : IPhotoProcessor
    {
        public string? Refusal { get; set; }

        public Result<ProcessedPhoto> Process(Stream upload) =>
            Refusal is null ? new ProcessedPhoto([0xFF, 0xD8], 1600, 1200) : Result.Failure<ProcessedPhoto>(Refusal);
    }

    private sealed class FixedUser(string id) : ICurrentUser
    {
        public string Id => id;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeStorage : IFileStorage
    {
        public Dictionary<string, (byte[] Content, string ContentType)> Files { get; } = [];

        public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            Files[key] = (buffer.ToArray(), contentType);
        }

        public Task<StoredFile?> OpenReadAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult(Files.TryGetValue(key, out var file)
                ? new StoredFile(new MemoryStream(file.Content), file.ContentType, file.Content.Length)
                : null);

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            Files.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRooms : IRoomRepository
    {
        private readonly List<Room> _pending = [];

        public bool FailOnSave { get; set; }
        public List<Room> Saved { get; } = [];

        public void Add(Room room) => _pending.Add(room);

        public Task<Room?> FindAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.SingleOrDefault(r => r.Id == id && r.OwnerId == ownerId));

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (FailOnSave)
                throw new InvalidOperationException("Database is down.");
            Saved.AddRange(_pending);
            _pending.Clear();
            return Task.CompletedTask;
        }
    }
}
