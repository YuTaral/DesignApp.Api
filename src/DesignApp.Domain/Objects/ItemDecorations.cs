using DesignApp.Domain.Exceptions;

using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Objects;

/// <summary>
/// The small things the user chose to put on an object, such as "cushions" and "throw blanket" on a sofa. The photo
/// shows them on it; nothing else is added. Two lists are equal when they hold the same names in the same order.
/// </summary>
public sealed record ItemDecorations
{
    /// <summary>No decorations.</summary>
    public static readonly ItemDecorations None = new([]);

    /// <summary>The decorations' names, trimmed, in the order chosen.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>Checks and keeps the decorations.</summary>
    /// <param name="names">Their names.</param>
    /// <exception cref="DomainException">One is empty, too long or repeated, or there are too many.</exception>
    public ItemDecorations(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        var trimmed = names.Select(n => n?.Trim() ?? "").ToList();
        var valid = trimmed.Count <= Constants.Plans.MaxDecorations
            && trimmed.All(n => n.Length is > 0 and <= Constants.Plans.MaxDecorationLength)
            && trimmed.Distinct(StringComparer.OrdinalIgnoreCase).Count() == trimmed.Count;

        if (!valid)
        {
            throw new DomainException(Constants.Messages.PlanItemDecorations);
        }

        Names = trimmed.AsReadOnly();
    }

    /// <summary>Whether there are none.</summary>
    public bool IsEmpty => Names.Count == 0;

    /// <inheritdoc/>
    public bool Equals(ItemDecorations? other)
    {
        return other is not null && Names.SequenceEqual(other.Names, StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return Names.Aggregate(0, (hash, name) => HashCode.Combine(hash, StringComparer.Ordinal.GetHashCode(name)));
    }
}
