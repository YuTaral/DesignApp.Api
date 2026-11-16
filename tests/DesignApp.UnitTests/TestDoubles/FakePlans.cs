using DesignApp.Application.Interfaces;
using DesignApp.Domain.Entities;

namespace DesignApp.UnitTests;

/// <summary>Built rooms kept in memory, with the saves counted.</summary>
internal sealed class FakePlans : IPlanRepository
{
    private readonly List<RoomPlan> _plans = [];

    /// <summary>How many times the changes were saved.</summary>
    public int Saves { get; private set; }

    /// <inheritdoc/>
    public void Add(RoomPlan plan)
    {
        _plans.Add(plan);
    }

    /// <inheritdoc/>
    public Task<RoomPlan?> FindAsync(Guid id, string ownerId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_plans.SingleOrDefault(p => p.Id == id && p.OwnerId == ownerId));
    }

    /// <inheritdoc/>
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        return Task.CompletedTask;
    }
}
