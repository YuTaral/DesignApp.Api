using DesignApp.Domain.Designs;

namespace DesignApp.Application.Abstractions;

/// <summary>Loads and saves designs in the database.</summary>
public interface IDesignRepository
{
    /// <summary>Tracks a new design; it is written on the next <see cref="SaveChangesAsync"/>.</summary>
    /// <param name="design">The design to add.</param>
    void Add(Design design);

    /// <summary>Marks a design for deletion on the next <see cref="SaveChangesAsync"/>.</summary>
    /// <param name="design">The design to delete.</param>
    void Remove(Design design);

    /// <summary>Finds a design that belongs to the given owner.</summary>
    /// <param name="id">The design's id.</param>
    /// <param name="ownerId">Only a design owned by this user is returned.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The design, or <c>null</c> when it does not exist or belongs to someone else.</returns>
    Task<Design?> FindAsync(Guid id, string ownerId, CancellationToken cancellationToken);

    /// <summary>Every design a user owns, most recently changed first.</summary>
    /// <param name="ownerId">The owner.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<Design>> ListByOwnerAsync(string ownerId, CancellationToken cancellationToken);

    /// <summary>Writes all added, changed and removed designs to the database.</summary>
    /// <param name="cancellationToken">Cancels the save.</param>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
