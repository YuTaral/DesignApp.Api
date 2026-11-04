using DesignApp.Application.Abstractions;
using DesignApp.Domain.Rooms;

namespace DesignApp.Application.Rooms;

public sealed class GetRoom(IRoomRepository rooms, ICurrentUser currentUser)
{
    /// <returns>The current user's room, or <c>null</c> if they have no room with that id.</returns>
    public Task<Room?> HandleAsync(Guid id, CancellationToken cancellationToken) =>
        rooms.FindAsync(id, currentUser.Id, cancellationToken);
}
