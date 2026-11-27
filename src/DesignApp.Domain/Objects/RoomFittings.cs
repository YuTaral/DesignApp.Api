using DesignApp.Domain.Exceptions;

using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Objects;

/// <summary>
/// How a built room is fitted, for the whole room: its cabinets' door style (and glass upper cabinets), their handles,
/// the metal finish of the handles, taps and fittings, the kitchen sink and tap, the vanity's basin, and how its tiles
/// are laid. Each cabinet keeps its own color and material.
/// </summary>
/// <param name="Fronts">The cabinets' door style.</param>
/// <param name="GlassUppers">Whether the upper cabinets have glass doors.</param>
/// <param name="Handles">The doors' and drawers' handles.</param>
/// <param name="Hardware">The metal finish of the handles, taps and fittings.</param>
/// <param name="Sink">The kitchen sink.</param>
/// <param name="Tap">The kitchen tap.</param>
/// <param name="Basin">The vanity's basin.</param>
/// <param name="Tiles">How the tiles are laid.</param>
public sealed record RoomFittings(CabinetFronts Fronts, bool GlassUppers, HandleStyle Handles, Hardware Hardware, KitchenSink Sink, KitchenTap Tap, Basin Basin,
    TilePattern Tiles)
{
    /// <summary>Slab doors with bar handles in brushed steel, an undermount sink with a gooseneck tap, an integrated basin, tiles in a grid.</summary>
    public static readonly RoomFittings Default = new(CabinetFronts.Slab, false, HandleStyle.Bar, Hardware.BrushedSteel, KitchenSink.Undermount, KitchenTap.Gooseneck,
        Basin.Integrated, TilePattern.Grid);

    /// <summary>The fittings, checked: every choice one of its list's.</summary>
    /// <exception cref="DomainException">A choice isn't one of its list's.</exception>
    public RoomFittings Checked()
    {
        var known = Enum.IsDefined(Fronts) && Enum.IsDefined(Handles) && Enum.IsDefined(Hardware) && Enum.IsDefined(Sink) && Enum.IsDefined(Tap)
            && Enum.IsDefined(Basin) && Enum.IsDefined(Tiles);

        if (!known)
        {
            throw new DomainException(Constants.Messages.PlanFittings);
        }

        return this;
    }
}
