using DesignApp.Application.Interfaces;
using DesignApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DesignApp.Infrastructure.Database;

/// <summary>The built rooms in Postgres.</summary>
/// <param name="db">The database.</param>
internal sealed class PlanRepository(AppDbContext db) : IPlanRepository
{
    /// <inheritdoc/>
    public void Add(RoomPlan plan)
    {
        db.RoomPlans.Add(plan);
    }

    /// <inheritdoc/>
    public Task<RoomPlan?> FindAsync(Guid id, string ownerId, CancellationToken cancellationToken)
    {
        return db.RoomPlans.SingleOrDefaultAsync(p => p.Id == id && p.OwnerId == ownerId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RoomPlan>> ListByOwnerAsync(string ownerId, CancellationToken cancellationToken)
    {
        return await db.RoomPlans.Where(p => p.OwnerId == ownerId).ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return db.SaveChangesAsync(cancellationToken);
    }
}
