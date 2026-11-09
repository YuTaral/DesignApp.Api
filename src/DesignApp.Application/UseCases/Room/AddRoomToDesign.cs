using DesignApp.Application.Interfaces;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;


namespace DesignApp.Application.UseCases.Room;

/// <summary>Adds one of the user's rooms to a design (saved from the editor, or just uploaded from the design's page).</summary>
/// <param name="rooms">Finds the room.</param>
/// <param name="designs">Loads and saves the design.</param>
/// <param name="currentUser">Whose rooms and designs these are.</param>
/// <param name="clock">Stamps the change.</param>
public sealed class AddRoomToDesign(IRoomRepository rooms, IDesignRepository designs, ICurrentUser currentUser, TimeProvider clock)
{
    /// <summary>Adds the room, optionally with its colors.</summary>
    /// <param name="designId">The design.</param>
    /// <param name="roomId">The room.</param>
    /// <param name="edits">Its colors; none for a room that is still being analyzed.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The design, or <c>null</c> when the user has no such design or room.</returns>
    /// <exception cref="DomainException">The room is in a design already, the design is full, or a color is invalid.</exception>
    public async Task<Design?> HandleAsync(Guid designId, Guid roomId, IReadOnlyList<EditOperation> edits, CancellationToken cancellationToken)
    {
        var design = await designs.FindAsync(designId, currentUser.Id, cancellationToken);
        var room = await rooms.FindAsync(roomId, currentUser.Id, cancellationToken);
        if (design is null || room is null)
            return null;
        if (await designs.IsRoomInADesignAsync(room.Id, cancellationToken))
            throw new DomainException(Constants.Messages.RoomInADesign);

        var now = clock.GetUtcNow();
        design.AddRoom(room, now);
        design.SetRoomEdits(room, edits, now);
        await designs.SaveChangesAsync(cancellationToken);
        return design;
    }
}
