using System.Globalization;
using DesignApp.Domain.Exceptions;

namespace DesignApp.Domain.Objects;

/// <summary>An sRGB color stored as an upper-case "#RRGGBB" string.</summary>
public readonly record struct HexColor
{
    /// <summary>The color as "#RRGGBB", upper case.</summary>
    public string Value { get; }

    private HexColor(string value) => Value = value;

    /// <summary>Parses a "#RRGGBB" string (any case).</summary>
    /// <param name="input">The text to parse.</param>
    /// <exception cref="DomainException"><paramref name="input"/> is not a #RRGGBB color.</exception>
    public static HexColor Parse(string input)
    {
        if (!TryParse(input, out var color))
            throw new DomainException(Constants.Messages.NotAColor(input));
        return color;
    }

    /// <summary>Parses a "#RRGGBB" string (any case) without throwing.</summary>
    /// <param name="input">The text to parse.</param>
    /// <param name="color">The parsed color, or <c>default</c> on failure.</param>
    /// <returns><c>true</c> when <paramref name="input"/> is a valid color.</returns>
    public static bool TryParse(string? input, out HexColor color)
    {
        color = default;
        if (input is null || input.Length != Constants.Colors.Length || input[0] != Constants.Colors.Prefix)
            return false;
        if (!int.TryParse(input.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
            return false;
        color = new HexColor(input.ToUpperInvariant());
        return true;
    }

    /// <summary>Returns <see cref="Value"/>.</summary>
    public override string ToString() => Value;
}
