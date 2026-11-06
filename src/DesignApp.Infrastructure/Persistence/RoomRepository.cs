using DesignApp.Application.Abstractions;
using DesignApp.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using static DesignApp.Domain.Constants;

namespace DesignApp.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IRoomRepository"/>.</summary>
/// <param name="db">The database session.</param>
internal sealed class RoomRepository(AppDbContext db) : IRoomRepository
{
    /// <inheritdoc/>
    public void Add(Room room) => db.Rooms.Add(room);

    /// <inheritdoc/>
    public void Remove(Room room) => db.Rooms.Remove(room);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Room>> ListUnsavedBeforeAsync(DateTimeOffset cutoff, int max, CancellationToken cancellationToken) =>
        await db.Rooms
            .Where(r => r.CreatedAt < cutoff && !db.Designs.Any(d => d.Rooms.Any(x => x.RoomId == r.Id)))
            .OrderBy(r => r.CreatedAt)
            .Take(max)
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<Room?> FindAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        db.Rooms.SingleOrDefaultAsync(r => r.Id == id && r.OwnerId == ownerId, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Room>> ListByOwnerAsync(string ownerId, CancellationToken cancellationToken) =>
        await db.Rooms
            .Where(r => r.OwnerId == ownerId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

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
    /// <remarks>
    /// When someone else saved the same room in the meantime (its version changed), changes to
    /// different fields are merged and the save is tried again: a guest signing in moves a room to
    /// the account (its owner) while the analysis is still writing its status. When both changed
    /// the same field, such as two workers moving one room's status, the save still fails.
    /// </remarks>
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException conflict) when (attempt < Constants.Persistence.MaxSaveAttempts)
            {
                foreach (var entry in conflict.Entries)
                {
                    if (!await TryMergeAsync(entry, cancellationToken))
                        throw;
                }
            }
        }
    }

    /// <summary>
    /// Takes the stored values of the fields this save didn't change, so only its own changes are
    /// written over the other writer's.
    /// </summary>
    /// <param name="entry">A room whose save conflicted.</param>
    /// <param name="cancellationToken">Cancels reading the stored row.</param>
    /// <returns>False when the row is gone or both writers changed the same field.</returns>
    private static async Task<bool> TryMergeAsync(EntityEntry entry, CancellationToken cancellationToken)
    {
        var stored = await entry.GetDatabaseValuesAsync(cancellationToken);
        if (stored is null)
            return false;

        foreach (var property in entry.Properties.Where(p => !p.Metadata.IsConcurrencyToken))
        {
            var theirs = stored[property.Metadata];
            if (!property.IsModified)
                property.CurrentValue = theirs;
            else if (!property.Metadata.GetValueComparer().Equals(theirs, property.OriginalValue))
                return false;
        }
        entry.OriginalValues.SetValues(stored);
        return true;
    }
}
