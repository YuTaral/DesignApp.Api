using DesignApp.Domain.Entities;

namespace DesignApp.Application.Interfaces;

/// <summary>Where the rooms users build are stored.</summary>
public interface IPlanRepository
{
    /// <summary>Adds a new plan, saved with the next <see cref="SaveChangesAsync"/>.</summary>
    /// <param name="plan">The plan.</param>
    void Add(RoomPlan plan);

    /// <summary>Finds a plan of an owner.</summary>
    /// <param name="id">The plan's id.</param>
    /// <param name="ownerId">The account or guest.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The plan, or <c>null</c> when that owner has no such plan.</returns>
    Task<RoomPlan?> FindAsync(Guid id, string ownerId, CancellationToken cancellationToken);

    /// <summary>A user's plans.</summary>
    /// <param name="ownerId">The user or guest.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<RoomPlan>> ListByOwnerAsync(string ownerId, CancellationToken cancellationToken);

    /// <summary>Saves the changes.</summary>
    /// <param name="cancellationToken">Cancels the save.</param>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
