using DesignApp.Application.Interfaces;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;


namespace DesignApp.Application.UseCases.Room;

/// <summary>Saves the colors of one room of a design.</summary>
/// <param name="rooms">Finds the room, to check the colors against its objects.</param>
/// <param name="designs">Loads and saves the design.</param>
/// <param name="currentUser">Whose designs may be changed.</param>
/// <param name="clock">Stamps the change.</param>
public sealed class SaveRoomColors(IRoomRepository rooms, IDesignRepository designs, ICurrentUser currentUser, TimeProvider clock)
{
    /// <summary>Replaces the room's colors in the design.</summary>
    /// <param name="designId">The design.</param>
    /// <param name="roomId">The room.</param>
    /// <param name="edits">The new colors.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The design, or <c>null</c> when the user has no such design, or the room isn't in it.</returns>
    /// <exception cref="DomainException">A color is invalid, or the room isn't analyzed yet.</exception>
    public async Task<Design?> HandleAsync(Guid designId, Guid roomId, IReadOnlyList<EditOperation> edits, CancellationToken cancellationToken)
    {
        var design = await designs.FindAsync(designId, currentUser.Id, cancellationToken);
        if (design is null || design.Rooms.All(r => r.RoomId != roomId))
            return null;
        var room = await rooms.FindAsync(roomId, currentUser.Id, cancellationToken);
        if (room is null)
            return null;

        design.SetRoomEdits(room, edits, clock.GetUtcNow());
        await designs.SaveChangesAsync(cancellationToken);
        return design;
    }
}
