using DesignApp.Application.Abstractions;
using DesignApp.Domain.Common;
using DesignApp.Domain.Designs;

namespace DesignApp.Application.Designs;

/// <summary>Creates a design from a room and its colors (saving from the editor).</summary>
/// <param name="rooms">Finds the room, checking it is the user's.</param>
/// <param name="designs">Stores the design.</param>
/// <param name="currentUser">Who saves it.</param>
/// <param name="clock">Stamps the design.</param>
public sealed class CreateDesign(IRoomRepository rooms, IDesignRepository designs, ICurrentUser currentUser, TimeProvider clock)
{
    /// <summary>Creates the design.</summary>
    /// <param name="roomId">Its first room.</param>
    /// <param name="name">What the user calls the design.</param>
    /// <param name="description">An optional short description.</param>
    /// <param name="edits">The room's colors.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The new design, or <c>null</c> when the user has no such room.</returns>
    /// <exception cref="DomainException">The room is in a design already, or the name, description or colors are invalid.</exception>
    public async Task<Design?> HandleAsync(
        Guid roomId, string name, string? description, IReadOnlyList<EditOperation> edits, CancellationToken cancellationToken)
    {
        var room = await rooms.FindAsync(roomId, currentUser.Id, cancellationToken);
        if (room is null)
            return null;
        if (await designs.IsRoomInADesignAsync(room.Id, cancellationToken))
            throw new DomainException(Constants.Messages.RoomInADesign);

        var design = Design.Create(room, currentUser.Id, name, description, edits, clock.GetUtcNow());
        designs.Add(design);
        await designs.SaveChangesAsync(cancellationToken);
        return design;
    }
}
