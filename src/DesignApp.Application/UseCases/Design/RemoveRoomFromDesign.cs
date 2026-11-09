using DesignApp.Application.Interfaces;
using DesignApp.Application.UseCases.Room;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;


namespace DesignApp.Application.UseCases.Design;

/// <summary>Takes a room out of a design and deletes it: rooms are only kept inside a design.</summary>
/// <param name="rooms">Finds the room.</param>
/// <param name="designs">Loads the design.</param>
/// <param name="remover">Deletes the room with its files.</param>
/// <param name="currentUser">Whose designs may be changed.</param>
/// <param name="clock">Stamps the change.</param>
public sealed class RemoveRoomFromDesign(
    IRoomRepository rooms, IDesignRepository designs, RoomRemover remover, ICurrentUser currentUser, TimeProvider clock)
{
    /// <summary>Removes and deletes the room.</summary>
    /// <param name="designId">The design.</param>
    /// <param name="roomId">The room.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The design, or <c>null</c> when the user has no such design, or the room isn't in it.</returns>
    /// <exception cref="DomainException">It is the design's last room.</exception>
    public async Task<Design?> HandleAsync(Guid designId, Guid roomId, CancellationToken cancellationToken)
    {
        var design = await designs.FindAsync(designId, currentUser.Id, cancellationToken);
        if (design is null || design.Rooms.All(r => r.RoomId != roomId))
            return null;

        design.RemoveRoom(roomId, clock.GetUtcNow());
        var room = await rooms.FindAsync(roomId, currentUser.Id, cancellationToken);
        // Saving the room's deletion saves the design's change too (one database session).
        if (room is not null)
            await remover.DeleteAsync([room], cancellationToken);
        else
            await designs.SaveChangesAsync(cancellationToken);
        return design;
    }
}
