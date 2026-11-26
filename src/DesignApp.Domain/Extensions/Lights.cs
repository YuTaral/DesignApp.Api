using System.Text.RegularExpressions;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Extensions;

/// <summary>
/// Which objects of a built room give light, and whether they are on: lamps, ceiling lights, and anything whose name or
/// decorations name a lamp or a light (a wall sconce, a sideboard with a table lamp, cabinets with under-cabinet lights).
/// </summary>
public static partial class Lights
{
    /// <summary>Whether an object gives light, so it has an On switch.</summary>
    /// <param name="item">The object.</param>
    public static bool GivesLight(this PlanItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.Shape is ItemShape.Lamp || item.Shape.IsCeilingLight() || Named().IsMatch(item.Label) || item.Decorations.Names.Any(Named().IsMatch);
    }

    /// <summary>Whether a light is on: as switched, or else in the evening.</summary>
    /// <param name="item">The object.</param>
    /// <param name="time">When the photo is taken.</param>
    public static bool IsOn(this PlanItem item, TimeOfDay time)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.GivesLight() && (item.Lit ?? time is TimeOfDay.Evening);
    }

    /// <summary>
    /// How many recessed spotlights a spotlights object holds across its width and depth: one every
    /// <see cref="Plans.SpotSpacing"/>, its size spanning the outer ones (a small one is a single spotlight).
    /// </summary>
    /// <param name="item">The spotlights.</param>
    public static (int Across, int Deep) Spots(this PlanItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return ((int)Math.Floor(item.Width / Plans.SpotSpacing) + 1, (int)Math.Floor(item.Depth / Plans.SpotSpacing) + 1);
    }

    /// <summary>How many spot heads a track light has along its length: one every <see cref="Plans.TrackHeadSpacing"/>.</summary>
    /// <param name="item">The track light.</param>
    public static int TrackHeads(this PlanItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return Math.Max(1, (int)Math.Round(item.Width / Plans.TrackHeadSpacing));
    }

    [GeneratedRegex(Plans.LightWords, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Named();
}
