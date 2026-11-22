using DesignApp.Domain.Exceptions;

using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Objects;

/// <summary>
/// The parts of an L- or P-shaped object, each sized on its own, in centimetres before turning: the run along its whole
/// back (as long as the object is wide), the corner leg running forward from the run at its right end (or its left end,
/// mirrored), and, for a P, the chaise the leg widens into at its front end. Seen from above, the back run is at the top
/// and the leg on the right unless <paramref name="LegLeft"/>.
/// </summary>
/// <param name="RunDepth">How deep the back run is (front to back).</param>
/// <param name="LegWidth">How wide the corner leg is (left to right).</param>
/// <param name="ChaiseWidth">How wide a P's chaise is, from the leg's side of the object (0 for an L).</param>
/// <param name="ChaiseDepth">How deep a P's chaise is, from the object's front (0 for an L).</param>
/// <param name="LegLeft">Whether the leg (and a P's chaise) is at the run's left end: the whole outline mirrored.</param>
public sealed record ItemParts(double RunDepth, double LegWidth, double ChaiseWidth = 0, double ChaiseDepth = 0, bool LegLeft = false)
{
    /// <summary>Which way the leg's side is from the object's middle before turning: 1 to the right, -1 to the left.</summary>
    public int Side => LegLeft ? -1 : 1;

    /// <summary>The parts an object of this outline and size starts with: runs a share of its smaller side deep.</summary>
    /// <param name="form">The outline: L-shaped or P-shaped.</param>
    /// <param name="width">The object's width.</param>
    /// <param name="depth">The object's depth.</param>
    public static ItemParts Default(ItemForm form, double width, double depth)
    {
        var smaller = Math.Min(width, depth);

        if (form != ItemForm.PShaped)
        {
            var run = Math.Round(smaller * Constants.Plans.LegShare);

            return new ItemParts(run, run);
        }

        var leg = Math.Round(smaller * Constants.Plans.PLegShare);

        return new ItemParts(leg, leg, Math.Min(Math.Round(leg * Constants.Plans.ChaiseShare), width), leg);
    }

    /// <summary>Checks the parts fit an object of this outline and size, each at least a few centimetres.</summary>
    /// <param name="form">The outline: L-shaped or P-shaped.</param>
    /// <param name="width">The object's width.</param>
    /// <param name="depth">The object's depth.</param>
    /// <exception cref="DomainException">A part is too small, too big, or the parts overlap past the object.</exception>
    public void Check(ItemForm form, double width, double depth)
    {
        var min = Constants.Plans.MinPartCentimetres;
        var sizes = new[] { RunDepth, LegWidth, ChaiseWidth, ChaiseDepth };
        var fits = sizes.All(double.IsFinite)
            && RunDepth >= min && RunDepth <= depth - min
            && LegWidth >= min && LegWidth <= width - min
            && (form == ItemForm.PShaped
                ? ChaiseWidth >= LegWidth && ChaiseWidth <= width && ChaiseDepth >= min && ChaiseDepth <= depth - RunDepth
                : ChaiseWidth == 0 && ChaiseDepth == 0);

        if (!fits)
        {
            throw new DomainException(Constants.Messages.PlanItemParts);
        }
    }
}
