using DesignApp.Domain.Common;

namespace DesignApp.Domain.Designs;

/// <summary>
/// One non-destructive change to an object in the room photo. Designs store these instead of
/// edited pixels, so later operations (resize, remove) slot in as new subtypes.
/// </summary>
/// <param name="ObjectIndex">The <see cref="Rooms.DetectedObject.Index"/> of the object to change.</param>
public abstract record EditOperation(int ObjectIndex);

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
        if (strength is < 0 or > 1 || double.IsNaN(strength))
            throw new DomainException("Recolor strength must be between 0 and 1.");
        Color = color;
        Strength = strength;
    }
}
