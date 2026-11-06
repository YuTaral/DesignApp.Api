using DesignApp.Application.Abstractions;
using DesignApp.Application.Analysis;
using DesignApp.Domain.Rooms;
using DesignApp.Infrastructure.Ai;
using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests;

/// <summary>A current user with a fixed id.</summary>
/// <param name="id">The user's id.</param>
/// <param name="signedIn">Whether it is an account rather than a guest.</param>
internal sealed class FixedUser(string id, bool signedIn = true) : ICurrentUser
{
    /// <inheritdoc/>
    public string Id => id;

    /// <inheritdoc/>
    public bool IsSignedIn => signedIn;
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
    private readonly List<Room> _removed = [];

    /// <summary>When set, saving throws as if the database were down.</summary>
    public bool FailOnSave { get; set; }
    /// <summary>Rooms written by a successful save.</summary>
    public List<Room> Saved { get; } = [];
    /// <summary>How many saves succeeded.</summary>
    public int SaveCount { get; private set; }
    /// <summary>Rooms that are in a design (the real repository asks the designs table).</summary>
    public HashSet<Guid> InDesigns { get; } = [];

    /// <inheritdoc/>
    public void Add(Room room) => _pending.Add(room);

    /// <inheritdoc/>
    public void Remove(Room room) => _removed.Add(room);

    /// <inheritdoc/>
    public Task<IReadOnlyList<Room>> ListUnsavedBeforeAsync(DateTimeOffset cutoff, int max, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Room>>([.. Saved.Where(r => r.CreatedAt < cutoff && !InDesigns.Contains(r.Id)).OrderBy(r => r.CreatedAt).Take(max)]);

    /// <inheritdoc/>
    public Task<Room?> FindAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        Task.FromResult(Saved.SingleOrDefault(r => r.Id == id && r.OwnerId == ownerId));

    /// <inheritdoc/>
    public Task<IReadOnlyList<Room>> ListByOwnerAsync(string ownerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Room>>([.. Saved.Where(r => r.OwnerId == ownerId).OrderByDescending(r => r.CreatedAt)]);

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
        Saved.RemoveAll(_removed.Contains);
        _removed.Clear();
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

/// <summary>The same providers for every AI mode, so a test decides what "the AI" does.</summary>
/// <param name="inventory">Step 1.</param>
/// <param name="segmentation">Step 2.</param>
internal sealed class FixedAiProviders(IObjectInventoryProvider inventory, ISegmentationProvider segmentation) : IAiProviders
{
    /// <inheritdoc/>
    public AiAvailability Availability { get; } = new(AiMode.Fake, CanChoose: true, RealAvailable: true);

    /// <summary>The modes rooms asked for, in order.</summary>
    public List<AiMode> Asked { get; } = [];

    /// <inheritdoc/>
    public IObjectInventoryProvider Inventory(AiMode mode)
    {
        Asked.Add(mode);
        return inventory;
    }

    /// <inheritdoc/>
    public ISegmentationProvider Segmentation(AiMode mode) => segmentation;
}

/// <summary>AI settings that never change, without a database.</summary>
/// <param name="settings">The settings to return; the defaults when omitted.</param>
internal sealed class FixedAiSettings(AiSettings? settings = null) : IAiSettingsStore
{
    /// <summary>The settings every call returns.</summary>
    public AiSettings Settings { get; } = settings ?? new AiSettings();

    /// <inheritdoc/>
    public Task<AiSettings> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Settings);
}
