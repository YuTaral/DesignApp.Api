using DesignApp.Domain.Exceptions;


namespace DesignApp.Domain.Objects;

/// <summary>
/// Paints an object a new color while keeping its shading and texture. An object split by color has
/// <see cref="Tones"/>, each recolored on its own; its <see cref="Color"/>, <see cref="Strength"/> and
/// <see cref="Paint"/> then repeat its first recolored tone, so a summary of the design can show it.
/// </summary>
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

    /// <summary>The object's tones when it is split by color (bricks and mortar); empty when it is one color.</summary>
    public IReadOnlyList<ToneRecolor> Tones { get; }

    /// <summary>Creates a recolor edit.</summary>
    /// <param name="objectIndex">The object to recolor.</param>
    /// <param name="color">The new color.</param>
    /// <param name="strength">How strongly the new color replaces the original, from 0 to 1.</param>
    /// <param name="paint">The real paint the color is, as <c>palette/paint</c>, or <c>null</c>.</param>
    /// <param name="tones">The object's tones when it is split by color, or <c>null</c> for one color.</param>
    /// <exception cref="DomainException">
    /// <paramref name="strength"/> is outside 0..1, the paint reference is malformed, or the tones are not 2 to 4 different
    /// colors whose shares add up to at most the whole object.
    /// </exception>
    public RecolorEdit(int objectIndex, HexColor color, double strength, string? paint = null, IReadOnlyList<ToneRecolor>? tones = null)
        : base(objectIndex)
    {
        if (strength is < Constants.Colors.MinStrength or > Constants.Colors.MaxStrength || double.IsNaN(strength))
            throw new DomainException(Constants.Messages.StrengthRange);
        if (paint is not null && !IsPaintReference(paint))
            throw new DomainException(Constants.Messages.PaintReference);
        Color = color;
        Strength = strength;
        Paint = paint;
        Tones = CheckTones(tones ?? []);
    }

    /// <summary>Whether two recolors are the same, tones included.</summary>
    /// <param name="other">The other recolor.</param>
    public bool Equals(RecolorEdit? other)
    {
        return other is not null && base.Equals(other) && Color == other.Color && Strength.Equals(other.Strength)
            && Paint == other.Paint && Tones.SequenceEqual(other.Tones);
    }

    /// <summary>A hash that agrees with <see cref="Equals(RecolorEdit)"/>.</summary>
    public override int GetHashCode()
    {
        return Tones.Aggregate(HashCode.Combine(base.GetHashCode(), Color, Strength, Paint), HashCode.Combine);
    }

    /// <summary>Checks an object's tones.</summary>
    /// <param name="tones">The tones.</param>
    /// <exception cref="DomainException">The tones are not 2 to 4 different colors whose shares fit in the object.</exception>
    private static List<ToneRecolor> CheckTones(IReadOnlyList<ToneRecolor> tones)
    {
        if (tones.Count == 0)
            return [];
        if (tones.Count is < Constants.Tones.Min or > Constants.Tones.Max)
            throw new DomainException(Constants.Messages.ToneCount);
        if (tones.Select(t => t.Center).Distinct().Count() != tones.Count)
            throw new DomainException(Constants.Messages.DuplicateTones);
        if (tones.Sum(t => t.Share) > 1 + Constants.Tones.ShareTolerance)
            throw new DomainException(Constants.Messages.ToneShare);
        return [.. tones];
    }

    /// <summary>Whether a paint reference has a palette and a paint, and fits.</summary>
    /// <param name="paint">The reference.</param>
    internal static bool IsPaintReference(string paint)
    {
        var separator = paint.IndexOf(Constants.Colors.PaintSeparator);
        return paint.Length <= Constants.Colors.MaxPaintLength && separator > 0 && separator < paint.Length - 1
            && !paint.Any(char.IsWhiteSpace);
    }
}
