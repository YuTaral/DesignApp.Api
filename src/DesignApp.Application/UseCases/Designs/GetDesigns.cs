using DesignApp.Application.Interfaces;
using DesignApp.Domain.Entities;


namespace DesignApp.Application.UseCases;

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
