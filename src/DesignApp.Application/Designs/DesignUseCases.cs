using DesignApp.Application.Abstractions;
using DesignApp.Domain.Designs;

namespace DesignApp.Application.Designs;

/// <summary>Saves a room's recolors as a new named design.</summary>
/// <param name="rooms">Finds the room, checking it is the user's.</param>
/// <param name="designs">Stores the design.</param>
/// <param name="currentUser">Who saves it.</param>
/// <param name="clock">Stamps the design.</param>
public sealed class CreateDesign(IRoomRepository rooms, IDesignRepository designs, ICurrentUser currentUser, TimeProvider clock)
{
    /// <summary>Creates the design.</summary>
    /// <param name="roomId">The restyled room.</param>
    /// <param name="name">What the user calls the design.</param>
    /// <param name="edits">The recolors.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The new design, or <c>null</c> when the user has no such room.</returns>
    /// <exception cref="Domain.Common.DomainException">The room isn't ready, or the name or edits are invalid.</exception>
    public async Task<Design?> HandleAsync(Guid roomId, string name, IReadOnlyList<EditOperation> edits, CancellationToken cancellationToken)
    {
        var room = await rooms.FindAsync(roomId, currentUser.Id, cancellationToken);
        if (room is null)
            return null;

        var design = Design.Create(room, currentUser.Id, name, edits, clock.GetUtcNow());
        designs.Add(design);
        await designs.SaveChangesAsync(cancellationToken);
        return design;
    }
}

/// <summary>Renames a design or replaces its recolors.</summary>
/// <param name="rooms">Finds the design's room, to check the edits against it.</param>
/// <param name="designs">Loads and saves the design.</param>
/// <param name="currentUser">Whose designs may be changed.</param>
/// <param name="clock">Stamps the change.</param>
public sealed class UpdateDesign(IRoomRepository rooms, IDesignRepository designs, ICurrentUser currentUser, TimeProvider clock)
{
    /// <summary>Updates the design.</summary>
    /// <param name="id">The design.</param>
    /// <param name="name">Its new name.</param>
    /// <param name="edits">Its new recolors, replacing the old ones.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The updated design, or <c>null</c> when the user has no such design.</returns>
    /// <exception cref="Domain.Common.DomainException">The name or edits are invalid.</exception>
    public async Task<Design?> HandleAsync(Guid id, string name, IReadOnlyList<EditOperation> edits, CancellationToken cancellationToken)
    {
        var design = await designs.FindAsync(id, currentUser.Id, cancellationToken);
        if (design is null)
            return null;
        var room = await rooms.FindAsync(design.RoomId, currentUser.Id, cancellationToken);
        if (room is null)
            return null;

        design.Update(room, name, edits, clock.GetUtcNow());
        await designs.SaveChangesAsync(cancellationToken);
        return design;
    }
}

/// <summary>Reads the user's designs.</summary>
/// <param name="designs">Loads the designs.</param>
/// <param name="currentUser">Whose designs are read.</param>
public sealed class GetDesigns(IDesignRepository designs, ICurrentUser currentUser)
{
    /// <summary>One design.</summary>
    /// <param name="id">The design.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The design, or <c>null</c> when the user has no such design.</returns>
    public Task<Design?> OneAsync(Guid id, CancellationToken cancellationToken) =>
        designs.FindAsync(id, currentUser.Id, cancellationToken);

    /// <summary>All of the user's designs, most recently changed first.</summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    public Task<IReadOnlyList<Design>> AllAsync(CancellationToken cancellationToken) =>
        designs.ListByOwnerAsync(currentUser.Id, cancellationToken);
}

/// <summary>Deletes a design. The room and its photo stay.</summary>
/// <param name="designs">Loads and deletes the design.</param>
/// <param name="currentUser">Whose designs may be deleted.</param>
public sealed class DeleteDesign(IDesignRepository designs, ICurrentUser currentUser)
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
        designs.Remove(design);
        await designs.SaveChangesAsync(cancellationToken);
        return true;
    }
}
