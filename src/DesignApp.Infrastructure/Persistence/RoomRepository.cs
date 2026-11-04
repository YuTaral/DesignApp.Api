using DesignApp.Application.Abstractions;
using DesignApp.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace DesignApp.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IRoomRepository"/>.</summary>
/// <param name="db">The database session.</param>
internal sealed class RoomRepository(AppDbContext db) : IRoomRepository
{
    /// <inheritdoc/>
    public void Add(Room room) => db.Rooms.Add(room);

    /// <inheritdoc/>
    public Task<Room?> FindAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        db.Rooms.SingleOrDefaultAsync(r => r.Id == id && r.OwnerId == ownerId, cancellationToken);

    /// <inheritdoc/>
    public Task<Room?> GetForAnalysisAsync(Guid id, CancellationToken cancellationToken) =>
        db.Rooms.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Guid>> ListUnfinishedAsync(CancellationToken cancellationToken) =>
        await db.Rooms
            .Where(r => r.Status != RoomStatus.Ready && r.Status != RoomStatus.Failed)
            .OrderBy(r => r.CreatedAt)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
