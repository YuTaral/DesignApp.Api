using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Api.Requests;

/// <summary>What a window is dressed with, as sent and as returned.</summary>
/// <param name="Kind">Curtains (sheer, full or both) or blinds (roller, Roman, venetian or vertical).</param>
/// <param name="Color">Its color as #RRGGBB.</param>
/// <param name="Fabric">What it is made of, such as "linen"; none when missing or empty.</param>
/// <param name="Length">How far curtains hang: to the sill or the floor.</param>
public sealed record WindowCoveringRequest(CoveringKind Kind, string? Color, string? Fabric, CurtainLength Length)
{
    /// <summary>The covering as the domain keeps it.</summary>
    /// <exception cref="Domain.Exceptions.DomainException">The kind, color, fabric or length is invalid.</exception>
    public WindowCovering ToDomain()
    {
        return new WindowCovering(Kind, HexColor.Parse(Color ?? ""), Fabric, Length);
    }

    /// <summary>A covering as the API sends it.</summary>
    /// <param name="covering">The covering; <c>null</c> for none.</param>
    public static WindowCoveringRequest? From(WindowCovering? covering)
    {
        return covering is null ? null : new(covering.Kind, covering.Color.Value, covering.Fabric, covering.Length);
    }
}
