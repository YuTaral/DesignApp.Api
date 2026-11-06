using DesignApp.Application.Abstractions;
using DesignApp.Domain.Rooms;
using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests;

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
