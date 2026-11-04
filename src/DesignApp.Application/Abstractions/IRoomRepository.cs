using DesignApp.Domain.Rooms;

namespace DesignApp.Application.Abstractions;

public interface IRoomRepository
{
    void Add(Room room);

    /// <returns>The room, or <c>null</c> when it does not exist or belongs to someone else.</returns>
    Task<Room?> FindAsync(Guid id, string ownerId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
