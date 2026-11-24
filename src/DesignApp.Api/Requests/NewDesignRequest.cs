using DesignApp.Domain.Objects;

namespace DesignApp.Api.Requests;

/// <summary>A new design a room is built for: made, with these details, when the room's first photo is made.</summary>
/// <param name="Name">Its name, 1 to 80 characters.</param>
/// <param name="Description">Its description, at most 300 characters; none when missing.</param>
public sealed record NewDesignRequest(string Name, string? Description = null)
{
    /// <summary>The details as the domain keeps them.</summary>
    /// <exception cref="Domain.Exceptions.DomainException">The name or description is too long, or the name is empty.</exception>
    public DesignDetails ToDomain()
    {
        return new DesignDetails(Name, Description);
    }
}
