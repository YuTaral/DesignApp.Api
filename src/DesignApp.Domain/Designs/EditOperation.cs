using DesignApp.Domain.Common;

namespace DesignApp.Domain.Designs;

/// <summary>
/// One non-destructive change to an object in the room photo. Designs store these instead of
/// edited pixels, so later operations (resize, remove) slot in as new subtypes.
/// </summary>
public abstract record EditOperation(int ObjectIndex);

public sealed record RecolorEdit : EditOperation
{
    public HexColor Color { get; }

    /// <summary>How strongly the new color replaces the original, from 0 to 1.</summary>
    public double Strength { get; }

    public RecolorEdit(int objectIndex, HexColor color, double strength) : base(objectIndex)
    {
        if (strength is < 0 or > 1 || double.IsNaN(strength))
            throw new DomainException("Recolor strength must be between 0 and 1.");
        Color = color;
        Strength = strength;
    }
}
