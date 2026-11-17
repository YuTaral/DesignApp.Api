using DesignApp.Application.Interfaces;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;

namespace DesignApp.Application.Helpers;

/// <summary>
/// Checks the parts of a room's edits only the application knows about: that every paint an edit or one of its tones
/// names exists, and that its color is that paint's color, so a saved paint name never describes another color.
/// </summary>
/// <param name="palettes">The palettes the paints come from.</param>
public sealed class EditChecks(IPaletteCatalog palettes)
{
    /// <summary>Checks the edits' paints.</summary>
    /// <param name="edits">The edits.</param>
    /// <exception cref="DomainException">A paint is unknown, or an edit's color isn't its paint's color.</exception>
    public void Check(IEnumerable<EditOperation> edits)
    {
        foreach (var edit in edits.OfType<RecolorEdit>())
        {
            CheckPaint(edit.Paint, edit.Color);

            foreach (var tone in edit.Tones)
            {
                CheckPaint(tone.Paint, tone.Color);
            }
        }
    }

    /// <summary>Checks that a paint exists and has the given color.</summary>
    /// <param name="reference">The paint, or <c>null</c> for none.</param>
    /// <param name="color">The color it was given with.</param>
    /// <exception cref="DomainException">The paint is unknown, or its color differs.</exception>
    private void CheckPaint(string? reference, HexColor? color)
    {
        if (reference is null)
        {
            return;
        }

        var paint = palettes.Find(reference) ?? throw new DomainException(Constants.Messages.UnknownPaint(reference));

        if (paint.Color != color)
        {
            throw new DomainException(Constants.Messages.PaintColorMismatch(paint.Code));
        }
    }
}
