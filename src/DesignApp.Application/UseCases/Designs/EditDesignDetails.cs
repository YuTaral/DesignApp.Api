using DesignApp.Application.Interfaces;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;


namespace DesignApp.Application.UseCases;

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
