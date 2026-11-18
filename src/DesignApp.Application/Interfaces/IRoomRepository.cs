using DesignApp.Domain.Entities;


namespace DesignApp.Application.Interfaces;

/// <summary>Loads and saves rooms in the database.</summary>
public interface IRoomRepository
{
    /// <summary>Tracks a new room; it is written on the next <see cref="SaveChangesAsync"/>.</summary>
    /// <param name="room">The room to add.</param>
    void Add(Room room);

    /// <summary>Marks a room for deletion on the next <see cref="SaveChangesAsync"/>.</summary>
    /// <param name="room">The room to delete.</param>
    void Remove(Room room);

    /// <summary>
    /// Rooms uploaded before <paramref name="cutoff"/> that aren't in any design, oldest first: the
    /// uploads nobody saved, which are deleted after a while.
    /// </summary>
    /// <param name="cutoff">Only rooms uploaded before this time.</param>
    /// <param name="max">At most this many.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<Room>> ListUnsavedBeforeAsync(DateTimeOffset cutoff, int max, CancellationToken cancellationToken);

    /// <summary>Finds a room that belongs to the given owner.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="ownerId">Only a room owned by this user is returned.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The room, or <c>null</c> when it does not exist or belongs to someone else.</returns>
    Task<Room?> FindAsync(Guid id, string ownerId, CancellationToken cancellationToken);

    /// <summary>Every room a user owns, newest first.</summary>
    /// <param name="ownerId">The owner.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<Room>> ListByOwnerAsync(string ownerId, CancellationToken cancellationToken);

    /// <summary>Loads a room for background work, without checking who owns it. Never use this for a request.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The room, or <c>null</c> when it does not exist.</returns>
    Task<Room?> GetForAnalysisAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Finds rooms whose analysis hasn't finished, such as ones cut off when the API stopped.</summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>Their ids, oldest first.</returns>
    Task<IReadOnlyList<Guid>> ListUnfinishedAsync(CancellationToken cancellationToken);

    /// <summary>Adds a photo of a room to choose from; saved with the next <see cref="SaveChangesAsync"/>.</summary>
    /// <param name="take">The photo.</param>
    void AddTake(PhotoTake take);

    /// <summary>A room's photos to choose from, in the order they were made.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<PhotoTake>> ListTakesAsync(Guid roomId, CancellationToken cancellationToken);

    /// <summary>One photo of a room, or <c>null</c> when the room has no such photo.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="takeId">The photo.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<PhotoTake?> FindTakeAsync(Guid roomId, Guid takeId, CancellationToken cancellationToken);

    /// <summary>Writes all added and changed rooms to the database.</summary>
    /// <param name="cancellationToken">Cancels the save.</param>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
