using DesignApp.Domain.Exceptions;

using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Objects;

/// <summary>
/// The worktop on a kitchen unit, apart from the unit's own color: its material and color, and the backsplash behind it
/// (none, a low upstand of the worktop, the worktop running up the wall, tiles or glass).
/// </summary>
public sealed record Worktop
{
    /// <summary>What it is made of, such as "quartz" or "oak".</summary>
    public string Material { get; }

    /// <summary>Its color.</summary>
    public HexColor Color { get; }

    /// <summary>What covers the wall behind it.</summary>
    public BacksplashKind Backsplash { get; }

    /// <summary>The tiles' or glass's color (an upstand or the worktop up the wall is the worktop's).</summary>
    public HexColor BacksplashColor { get; }

    /// <summary>Creates a worktop.</summary>
    /// <param name="material">What it is made of.</param>
    /// <param name="color">Its color.</param>
    /// <param name="backsplash">What covers the wall behind it.</param>
    /// <param name="backsplashColor">The tiles' or glass's color.</param>
    /// <exception cref="DomainException">The material is missing or too long, or the backsplash isn't one of the list.</exception>
    public Worktop(string? material, HexColor color, BacksplashKind backsplash, HexColor backsplashColor)
    {
        var made = (material ?? "").Trim();

        if (made.Length is 0 or > Constants.Plans.MaxMaterialLength || !Enum.IsDefined(backsplash))
        {
            throw new DomainException(Constants.Messages.PlanWorktop);
        }

        (Material, Color, Backsplash, BacksplashColor) = (made, color, backsplash, backsplashColor);
    }
}
