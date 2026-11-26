using DesignApp.Domain.Exceptions;

using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Objects;

/// <summary>What a window of a built room is dressed with: curtains or blinds, their fabric, color and length.</summary>
public sealed record WindowCovering
{
    /// <summary>Curtains (sheer, full or both) or blinds (roller, Roman, venetian or vertical).</summary>
    public CoveringKind Kind { get; }

    /// <summary>Its color.</summary>
    public HexColor Color { get; }

    /// <summary>What it is made of, such as "linen" or "wood"; empty for none named.</summary>
    public string Fabric { get; }

    /// <summary>How far curtains hang: to the sill or to the floor (blinds cover the window).</summary>
    public CurtainLength Length { get; }

    /// <summary>Creates a covering.</summary>
    /// <param name="kind">Curtains or blinds.</param>
    /// <param name="color">Its color.</param>
    /// <param name="fabric">What it is made of; empty for none named.</param>
    /// <param name="length">How far curtains hang.</param>
    /// <exception cref="DomainException">The kind or length isn't one of the lists, or the fabric's name is too long.</exception>
    public WindowCovering(CoveringKind kind, HexColor color, string? fabric, CurtainLength length)
    {
        var made = (fabric ?? "").Trim();

        if (!Enum.IsDefined(kind) || !Enum.IsDefined(length) || made.Length > Constants.Plans.MaxMaterialLength)
        {
            throw new DomainException(Constants.Messages.PlanCovering);
        }

        (Kind, Color, Fabric, Length) = (kind, color, made, length);
    }

    /// <summary>Whether it is curtains (which hang to a length), not blinds.</summary>
    public bool IsCurtains => Kind is CoveringKind.Sheers or CoveringKind.Curtains or CoveringKind.SheersAndCurtains;
}
