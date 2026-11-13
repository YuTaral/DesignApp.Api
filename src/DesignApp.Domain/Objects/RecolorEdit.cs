using DesignApp.Domain.Exceptions;


namespace DesignApp.Domain.Objects;

/// <summary>Paints an object a new color while keeping its shading and texture.</summary>
public sealed record RecolorEdit : EditOperation
{
    /// <summary>The new color.</summary>
    public HexColor Color { get; }

    /// <summary>How strongly the new color replaces the original, from 0 to 1.</summary>
    public double Strength { get; }

    /// <summary>
    /// The real paint the color is, as <c>palette/paint</c> (such as <c>ral-classic/9010</c>); <c>null</c> for a color
    /// picked freely.
    /// </summary>
    public string? Paint { get; }

    /// <summary>Creates a recolor edit.</summary>
    /// <param name="objectIndex">The object to recolor.</param>
    /// <param name="color">The new color.</param>
    /// <param name="strength">How strongly the new color replaces the original, from 0 to 1.</param>
    /// <param name="paint">The real paint the color is, as <c>palette/paint</c>, or <c>null</c>.</param>
    /// <exception cref="DomainException"><paramref name="strength"/> is outside 0..1, or the paint reference is malformed.</exception>
    public RecolorEdit(int objectIndex, HexColor color, double strength, string? paint = null) : base(objectIndex)
    {
        if (strength is < Constants.Colors.MinStrength or > Constants.Colors.MaxStrength || double.IsNaN(strength))
            throw new DomainException(Constants.Messages.StrengthRange);
        if (paint is not null && !IsPaintReference(paint))
            throw new DomainException(Constants.Messages.PaintReference);
        Color = color;
        Strength = strength;
        Paint = paint;
    }

    /// <summary>Whether a paint reference has a palette and a paint, and fits.</summary>
    /// <param name="paint">The reference.</param>
    private static bool IsPaintReference(string paint)
    {
        var separator = paint.IndexOf(Constants.Colors.PaintSeparator);
        return paint.Length <= Constants.Colors.MaxPaintLength && separator > 0 && separator < paint.Length - 1
            && !paint.Any(char.IsWhiteSpace);
    }
}
