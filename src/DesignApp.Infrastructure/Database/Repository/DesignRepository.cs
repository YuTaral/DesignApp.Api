using DesignApp.Application.Abstractions;
using DesignApp.Domain.Designs;
using Microsoft.EntityFrameworkCore;

namespace DesignApp.Infrastructure.Database;

/// <summary>EF Core implementation of <see cref="IDesignRepository"/>. A design's rooms load with it (they're owned).</summary>
/// <param name="db">The database session.</param>
internal sealed class DesignRepository(AppDbContext db) : IDesignRepository
{
    /// <inheritdoc/>
    public void Add(Design design) => db.Designs.Add(design);

    /// <inheritdoc/>
    public void Remove(Design design) => db.Designs.Remove(design);

    /// <inheritdoc/>
    public Task<Design?> FindAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        db.Designs.SingleOrDefaultAsync(d => d.Id == id && d.OwnerId == ownerId, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Design>> ListByOwnerAsync(string ownerId, CancellationToken cancellationToken) =>
        await db.Designs
            .Where(d => d.OwnerId == ownerId)
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<bool> IsRoomInADesignAsync(Guid roomId, CancellationToken cancellationToken) =>
        db.Designs.AnyAsync(d => d.Rooms.Any(r => r.RoomId == roomId), cancellationToken);

    /// <inheritdoc/>
    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
