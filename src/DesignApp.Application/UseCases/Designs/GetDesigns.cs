using DesignApp.Application.Interfaces;
using DesignApp.Domain.Entities;


namespace DesignApp.Application.UseCases;

/// <summary>Reads the user's designs.</summary>
/// <param name="designs">Loads the designs.</param>
/// <param name="rooms">Loads the user's rooms, for the photo version each design room is on.</param>
/// <param name="currentUser">Whose designs are read.</param>
public sealed class GetDesigns(IDesignRepository designs, IRoomRepository rooms, ICurrentUser currentUser)
{
    /// <summary>One design.</summary>
    /// <param name="id">The design.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The design, or <c>null</c> when the user has no such design.</returns>
    public Task<Design?> OneAsync(Guid id, CancellationToken cancellationToken)
    {
        return designs.FindAsync(id, currentUser.Id, cancellationToken);
    }

    /// <summary>All of the user's designs, most recently changed first.</summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    public Task<IReadOnlyList<Design>> AllAsync(CancellationToken cancellationToken)
    {
        return designs.ListByOwnerAsync(currentUser.Id, cancellationToken);
    }

    /// <summary>
    /// The photo version each of the user's rooms is on (above 0 after objects were removed), so a design shows its
    /// rooms as they look now.
    /// </summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>Photo versions by room id.</returns>
    public async Task<IReadOnlyDictionary<Guid, int>> PhotoVersionsAsync(CancellationToken cancellationToken)
    {
        var owned = await rooms.ListByOwnerAsync(currentUser.Id, cancellationToken);
        return owned.ToDictionary(r => r.Id, r => r.PhotoVersion);
    }
}
