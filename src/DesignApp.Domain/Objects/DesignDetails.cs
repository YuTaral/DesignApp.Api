using DesignApp.Domain.Exceptions;

namespace DesignApp.Domain.Objects;

/// <summary>A design's name and description, trimmed and checked: a name of 1 to 80 characters, a description of at most 300.</summary>
public sealed record DesignDetails
{
    /// <summary>Checks and keeps a name and description.</summary>
    /// <param name="name">The name; it is trimmed.</param>
    /// <param name="description">The description, or null for none; it is trimmed.</param>
    /// <exception cref="DomainException">The name or description is too long, or the name is empty.</exception>
    public DesignDetails(string name, string? description)
    {
        var trimmedName = (name ?? "").Trim();
        var trimmedDescription = (description ?? "").Trim();

        if (trimmedName.Length is 0 or > Constants.Designs.MaxNameLength)
        {
            throw new DomainException(Constants.Messages.NameLength);
        }

        if (trimmedDescription.Length > Constants.Designs.MaxDescriptionLength)
        {
            throw new DomainException(Constants.Messages.DescriptionLength);
        }

        (Name, Description) = (trimmedName, trimmedDescription);
    }

    /// <summary>The name.</summary>
    public string Name { get; }

    /// <summary>The description; empty for none.</summary>
    public string Description { get; }
}
