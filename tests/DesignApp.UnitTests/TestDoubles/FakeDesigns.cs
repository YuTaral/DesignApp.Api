using DesignApp.Application.Interfaces;
using DesignApp.Domain.Entities;

namespace DesignApp.UnitTests;

/// <summary>In-memory designs.</summary>
internal sealed class FakeDesigns : IDesignRepository
{
    /// <summary>The designs, saved or not.</summary>
    public List<Design> All { get; } = [];

    /// <summary>How often the designs were saved.</summary>
    public int Saves { get; private set; }

    public void Add(Design design)
    {
        All.Add(design);
    }

    public void Remove(Design design)
    {
        All.Remove(design);
    }

    public Task<Design?> FindAsync(Guid id, string ownerId, CancellationToken cancellationToken)
    {
        return Task.FromResult(All.SingleOrDefault(d => d.Id == id && d.OwnerId == ownerId));
    }

    public Task<IReadOnlyList<Design>> ListByOwnerAsync(string ownerId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Design>>([.. All.Where(d => d.OwnerId == ownerId)]);
    }

    public Task<bool> IsRoomInADesignAsync(Guid roomId, CancellationToken cancellationToken)
    {
        return Task.FromResult(All.Any(d => d.Rooms.Any(r => r.RoomId == roomId)));
    }

    public Task<IReadOnlyList<Design>> ListHoldingRoomAsync(Guid roomId, string ownerId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Design>>([.. All.Where(d => d.OwnerId == ownerId && d.Rooms.Any(r => r.RoomId == roomId))]);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;

        return Task.CompletedTask;
    }
}
