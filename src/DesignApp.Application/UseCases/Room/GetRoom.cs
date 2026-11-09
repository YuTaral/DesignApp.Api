using DesignApp.Application.Interfaces;
using DesignApp.Domain.Entities;


namespace DesignApp.Application.UseCases.Room;

/// <summary>Use case: load one of the current user's rooms.</summary>
/// <param name="rooms">The room database.</param>
/// <param name="currentUser">Whose rooms may be returned.</param>
public sealed class GetRoom(IRoomRepository rooms, ICurrentUser currentUser)
{
    /// <summary>Loads the room.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The current user's room, or <c>null</c> if they have no room with that id.</returns>
    public Task<Room?> HandleAsync(Guid id, CancellationToken cancellationToken) =>
        rooms.FindAsync(id, currentUser.Id, cancellationToken);
}
