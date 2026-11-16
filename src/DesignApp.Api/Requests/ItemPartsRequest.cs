using DesignApp.Domain.Objects;

namespace DesignApp.Api.Requests;

/// <summary>
/// The parts of an L- or P-shaped object, as sent and as returned, in centimetres before turning: the back run's depth,
/// the corner leg's width, and a P's chaise (0 for an L).
/// </summary>
/// <param name="RunDepth">How deep the back run is.</param>
/// <param name="LegWidth">How wide the corner leg is.</param>
/// <param name="ChaiseWidth">How wide a P's chaise is; 0 for an L.</param>
/// <param name="ChaiseDepth">How deep a P's chaise is; 0 for an L.</param>
public sealed record ItemPartsRequest(double RunDepth, double LegWidth, double ChaiseWidth = 0, double ChaiseDepth = 0)
{
    /// <summary>The parts as the domain keeps them.</summary>
    public ItemParts ToDomain()
    {
        return new ItemParts(RunDepth, LegWidth, ChaiseWidth, ChaiseDepth);
    }

    /// <summary>The parts as the API sends them; <c>null</c> for an object without parts.</summary>
    /// <param name="parts">The parts.</param>
    public static ItemPartsRequest? From(ItemParts? parts)
    {
        return parts is null ? null : new ItemPartsRequest(parts.RunDepth, parts.LegWidth, parts.ChaiseWidth, parts.ChaiseDepth);
    }
}
