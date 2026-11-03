using System.Globalization;

namespace DesignApp.Domain.Common;

/// <summary>An sRGB color stored as an upper-case "#RRGGBB" string.</summary>
public readonly record struct HexColor
{
    public string Value { get; }

    private HexColor(string value) => Value = value;

    public static HexColor Parse(string input)
    {
        if (!TryParse(input, out var color))
            throw new DomainException($"'{input}' is not a #RRGGBB color.");
        return color;
    }

    public static bool TryParse(string? input, out HexColor color)
    {
        color = default;
        if (input is null || input.Length != 7 || input[0] != '#')
            return false;
        if (!int.TryParse(input.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
            return false;
        color = new HexColor(input.ToUpperInvariant());
        return true;
    }

    public override string ToString() => Value;
}
