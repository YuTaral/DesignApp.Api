using DesignApp.Application.Abstractions;
using DesignApp.Application.Rooms;

namespace DesignApp.Application.Designs;

/// <summary>Deletes a design and its rooms, with their photos and outlines.</summary>
/// <param name="rooms">Finds the rooms.</param>
/// <param name="designs">Loads and deletes the design.</param>
/// <param name="remover">Deletes the rooms with their files.</param>
/// <param name="currentUser">Whose designs may be deleted.</param>
public sealed class DeleteDesign(IRoomRepository rooms, IDesignRepository designs, RoomRemover remover, ICurrentUser currentUser)
{
    /// <summary>Deletes the design.</summary>
    /// <param name="id">The design.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>False when the user has no such design.</returns>
    public async Task<bool> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var design = await designs.FindAsync(id, currentUser.Id, cancellationToken);
        if (design is null)
            return false;

        var owned = new List<Domain.Rooms.Room>();
        foreach (var entry in design.Rooms)
        {
            if (await rooms.FindAsync(entry.RoomId, currentUser.Id, cancellationToken) is { } room)
                owned.Add(room);
        }
        designs.Remove(design);
        await remover.DeleteAsync(owned, cancellationToken);
        return true;
    }
}
