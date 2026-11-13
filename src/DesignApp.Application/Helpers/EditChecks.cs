using DesignApp.Application.Interfaces;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;

namespace DesignApp.Application.Helpers;

/// <summary>
/// Checks the parts of a room's edits only the application knows about: that every paint an edit names exists, and
/// that the edit's color is that paint's color, so a saved paint name never describes another color.
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
            if (edit.Paint is null)
                continue;
            var paint = palettes.Find(edit.Paint) ?? throw new DomainException(Constants.Messages.UnknownPaint(edit.Paint));
            if (paint.Color != edit.Color)
                throw new DomainException(Constants.Messages.PaintColorMismatch(paint.Code));
        }
    }
}
