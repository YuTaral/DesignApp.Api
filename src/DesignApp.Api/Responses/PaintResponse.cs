using DesignApp.Application.Objects;

namespace DesignApp.Api.Responses;

/// <summary>One paint of a palette.</summary>
/// <param name="Ref">What an edit stores to name it, such as <c>ral-classic/9010</c>.</param>
/// <param name="Code">The code shops use, such as <c>RAL 9010</c>.</param>
/// <param name="Name">The color's name, such as "Pure white".</param>
/// <param name="Color">Its color on screen as #RRGGBB; an edit naming the paint must use exactly this color.</param>
internal sealed record PaintResponse(string Ref, string Code, string Name, string Color)
{
    /// <summary>The response for one paint.</summary>
    /// <param name="palette">The paint's palette.</param>
    /// <param name="paint">The paint.</param>
    public static PaintResponse From(Palette palette, Paint paint)
    {
        return new(palette.ReferenceTo(paint), paint.Code, paint.Name, paint.Color.Value);
    }
}
