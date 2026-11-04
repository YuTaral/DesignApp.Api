using DesignApp.Application.Abstractions;
using DesignApp.Application.Analysis;
using DesignApp.Domain.Rooms;

namespace DesignApp.UnitTests;

/// <summary>A current user with a fixed id.</summary>
/// <param name="id">The user's id.</param>
internal sealed class FixedUser(string id) : ICurrentUser
{
    /// <inheritdoc/>
    public string Id => id;
}

/// <summary>A clock that always returns the same time.</summary>
/// <param name="now">The time to return.</param>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>In-memory file storage.</summary>
internal sealed class FakeStorage : IFileStorage
{
    /// <summary>The stored files by key.</summary>
    public Dictionary<string, (byte[] Content, string ContentType)> Files { get; } = [];

    /// <inheritdoc/>
    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        Files[key] = (buffer.ToArray(), contentType);
    }

    /// <inheritdoc/>
    public Task<StoredFile?> OpenReadAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(Files.TryGetValue(key, out var file)
            ? new StoredFile(new MemoryStream(file.Content), file.ContentType, file.Content.Length)
            : null);

    /// <inheritdoc/>
    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        Files.Remove(key);
        return Task.CompletedTask;
    }
}

/// <summary>In-memory room repository that records saves and can be told to fail them.</summary>
internal sealed class FakeRooms : IRoomRepository
{
    private readonly List<Room> _pending = [];

    /// <summary>When set, saving throws as if the database were down.</summary>
    public bool FailOnSave { get; set; }
    /// <summary>Rooms written by a successful save.</summary>
    public List<Room> Saved { get; } = [];
    /// <summary>How many saves succeeded.</summary>
    public int SaveCount { get; private set; }

    /// <inheritdoc/>
    public void Add(Room room) => _pending.Add(room);

    /// <inheritdoc/>
    public Task<Room?> FindAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        Task.FromResult(Saved.SingleOrDefault(r => r.Id == id && r.OwnerId == ownerId));

    /// <inheritdoc/>
    public Task<Room?> GetForAnalysisAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Saved.SingleOrDefault(r => r.Id == id));

    /// <inheritdoc/>
    public Task<IReadOnlyList<Guid>> ListUnfinishedAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>([.. Saved.Where(r => r.Status is not (RoomStatus.Ready or RoomStatus.Failed)).Select(r => r.Id)]);

    /// <inheritdoc/>
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (FailOnSave)
            throw new InvalidOperationException("Database is down.");
        Saved.AddRange(_pending);
        _pending.Clear();
        SaveCount++;
        return Task.CompletedTask;
    }
}

/// <summary>Records which rooms were scheduled for analysis.</summary>
internal sealed class RecordingScheduler : IAnalysisScheduler
{
    /// <summary>Scheduled room ids, in order.</summary>
    public List<Guid> Scheduled { get; } = [];

    /// <inheritdoc/>
    public void Schedule(Guid roomId) => Scheduled.Add(roomId);
}

/// <summary>Records the status of every room change sent to the browser.</summary>
internal sealed class RecordingNotifier : IRoomNotifier
{
    /// <summary>The statuses sent, in order.</summary>
    public List<RoomStatus> Sent { get; } = [];

    /// <inheritdoc/>
    public Task RoomChangedAsync(Room room, CancellationToken cancellationToken)
    {
        Sent.Add(room.Status);
        return Task.CompletedTask;
    }
}
