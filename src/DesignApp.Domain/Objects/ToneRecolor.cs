using DesignApp.Domain.Exceptions;

namespace DesignApp.Domain.Objects;

/// <summary>
/// One tone of an object split by color, such as the bricks or the mortar of a wall: the pixels nearest its
/// <see cref="Center"/> color, recolored on their own.
/// </summary>
public sealed record ToneRecolor
{
    /// <summary>The tone's original color in the photo; the object's pixels nearest it belong to it.</summary>
    public HexColor Center { get; }

    /// <summary>How much of the object the tone covers, from 0 to 1.</summary>
    public double Share { get; }

    /// <summary>The tone's new color, or <c>null</c> to keep it as in the photo.</summary>
    public HexColor? Color { get; }

    /// <summary>How strongly the new color replaces the original, from 0 to 1.</summary>
    public double Strength { get; }

    /// <summary>The real paint the color is, as <c>palette/paint</c>; <c>null</c> for a free color or none.</summary>
    public string? Paint { get; }

    /// <summary>Creates a tone.</summary>
    /// <param name="center">The tone's original color.</param>
    /// <param name="share">How much of the object it covers, from 0 to 1.</param>
    /// <param name="color">Its new color, or <c>null</c> to keep it.</param>
    /// <param name="strength">How strongly the new color replaces the original, from 0 to 1.</param>
    /// <param name="paint">The real paint the color is, or <c>null</c>.</param>
    /// <exception cref="DomainException">The share or strength is outside 0..1, the paint is malformed, or a paint has no color.</exception>
    public ToneRecolor(HexColor center, double share, HexColor? color, double strength, string? paint = null)
    {
        if (double.IsNaN(share) || share is < 0 or > 1)
            throw new DomainException(Constants.Messages.ToneShare);
        if (double.IsNaN(strength) || strength is < Constants.Colors.MinStrength or > Constants.Colors.MaxStrength)
            throw new DomainException(Constants.Messages.StrengthRange);
        if (paint is not null && color is null)
            throw new DomainException(Constants.Messages.TonePaintNeedsColor);
        if (paint is not null && !RecolorEdit.IsPaintReference(paint))
            throw new DomainException(Constants.Messages.PaintReference);
        (Center, Share, Color, Strength, Paint) = (center, share, color, strength, paint);
    }
}
