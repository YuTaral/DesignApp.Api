using DesignApp.Domain.Exceptions;


namespace DesignApp.Domain.Objects;

/// <summary>Paints an object a new color while keeping its shading and texture.</summary>
public sealed record RecolorEdit : EditOperation
{
    /// <summary>The new color.</summary>
    public HexColor Color { get; }

    /// <summary>How strongly the new color replaces the original, from 0 to 1.</summary>
    public double Strength { get; }

    /// <summary>Creates a recolor edit.</summary>
    /// <param name="objectIndex">The object to recolor.</param>
    /// <param name="color">The new color.</param>
    /// <param name="strength">How strongly the new color replaces the original, from 0 to 1.</param>
    /// <exception cref="DomainException"><paramref name="strength"/> is outside 0..1.</exception>
    public RecolorEdit(int objectIndex, HexColor color, double strength) : base(objectIndex)
    {
        if (strength is < Constants.Colors.MinStrength or > Constants.Colors.MaxStrength || double.IsNaN(strength))
            throw new DomainException(Constants.Messages.StrengthRange);
        Color = color;
        Strength = strength;
    }
}
