using DesignApp.Application.Objects;

namespace DesignApp.Api.Responses;

/// <summary>A paint palette with its paints.</summary>
/// <param name="Id">The palette's id, such as <c>ral-classic</c>.</param>
/// <param name="Name">The name people see, such as "RAL Classic".</param>
/// <param name="Notice">Where the colors come from and how exact they are.</param>
/// <param name="Paints">The paints, in the palette's order.</param>
internal sealed record PaletteResponse(string Id, string Name, string Notice, List<PaintResponse> Paints)
{
    /// <summary>The response for a palette.</summary>
    /// <param name="palette">The palette.</param>
    public static PaletteResponse From(Palette palette)
    {
        return new(palette.Id, palette.Name, palette.Notice, [.. palette.Paints.Select(p => PaintResponse.From(palette, p))]);
    }
}
