using DesignApp.Application.Interfaces;
using DesignApp.Domain.Entities;

namespace DesignApp.Application.UseCases;

/// <summary>Use case: a room's photos to choose from, in the order they were made.</summary>
/// <param name="rooms">The room database.</param>
/// <param name="currentUser">Whose rooms may be read.</param>
public sealed class GetRoomTakes(IRoomRepository rooms, ICurrentUser currentUser)
{
    /// <summary>Lists the photos.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The photos (none for a room without them), or <c>null</c> when the user has no such room.</returns>
    public async Task<IReadOnlyList<PhotoTake>?> HandleAsync(Guid roomId, CancellationToken cancellationToken)
    {
        var room = await rooms.FindAsync(roomId, currentUser.Id, cancellationToken);

        return room is null ? null : await rooms.ListTakesAsync(room.Id, cancellationToken);
    }
}
