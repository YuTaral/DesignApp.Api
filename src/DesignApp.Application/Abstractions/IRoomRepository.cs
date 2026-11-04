using DesignApp.Domain.Rooms;

namespace DesignApp.Application.Abstractions;

/// <summary>Loads and saves rooms in the database.</summary>
public interface IRoomRepository
{
    /// <summary>Tracks a new room; it is written on the next <see cref="SaveChangesAsync"/>.</summary>
    /// <param name="room">The room to add.</param>
    void Add(Room room);

    /// <summary>Finds a room that belongs to the given owner.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="ownerId">Only a room owned by this user is returned.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The room, or <c>null</c> when it does not exist or belongs to someone else.</returns>
    Task<Room?> FindAsync(Guid id, string ownerId, CancellationToken cancellationToken);

    /// <summary>Loads a room for background work, without checking who owns it. Never use this for a request.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The room, or <c>null</c> when it does not exist.</returns>
    Task<Room?> GetForAnalysisAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Finds rooms whose analysis hasn't finished, such as ones cut off when the API stopped.</summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>Their ids, oldest first.</returns>
    Task<IReadOnlyList<Guid>> ListUnfinishedAsync(CancellationToken cancellationToken);

    /// <summary>Writes all added and changed rooms to the database.</summary>
    /// <param name="cancellationToken">Cancels the save.</param>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
