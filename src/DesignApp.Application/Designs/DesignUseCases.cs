using DesignApp.Application.Abstractions;
using DesignApp.Application.Rooms;
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

/// <summary>Changes a design's name and description.</summary>
/// <param name="designs">Loads and saves the design.</param>
/// <param name="currentUser">Whose designs may be changed.</param>
/// <param name="clock">Stamps the change.</param>
public sealed class EditDesignDetails(IDesignRepository designs, ICurrentUser currentUser, TimeProvider clock)
{
    /// <summary>Saves the new details.</summary>
    /// <param name="id">The design.</param>
    /// <param name="name">Its new name.</param>
    /// <param name="description">Its new description, or null for none.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The design, or <c>null</c> when the user has no such design.</returns>
    /// <exception cref="DomainException">The name or description is invalid.</exception>
    public async Task<Design?> HandleAsync(Guid id, string name, string? description, CancellationToken cancellationToken)
    {
        var design = await designs.FindAsync(id, currentUser.Id, cancellationToken);
        if (design is null)
            return null;
        design.Rename(name, description, clock.GetUtcNow());
        await designs.SaveChangesAsync(cancellationToken);
        return design;
    }
}

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
