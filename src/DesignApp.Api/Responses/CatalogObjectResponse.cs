using DesignApp.Application.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Api.Responses;

/// <summary>An object the user can put in a built room, with its usual size and color, and its variations.</summary>
/// <param name="Key">A short id within the room type.</param>
/// <param name="Label">Its name.</param>
/// <param name="Shape">How it is drawn in 3D.</param>
/// <param name="Width">Its usual width, in centimetres.</param>
/// <param name="Depth">Its usual depth, in centimetres.</param>
/// <param name="Height">Its usual height, in centimetres.</param>
/// <param name="Color">Its usual color as #RRGGBB.</param>
/// <param name="Elevation">How high its bottom usually is above the floor, in centimetres (a window's sill; else 0).</param>
/// <param name="Form">Its outline from above.</param>
/// <param name="Variants">Its variations (shapes and sizes), the first being the object itself; empty for an object the AI described.</param>
/// <param name="Decorations">The most popular small things to put on it (up to five), offered when it is added; may be empty.</param>
public sealed record CatalogObjectResponse(string Key, string Label, ItemShape Shape, double Width, double Depth, double Height, string Color, double Elevation,
    ItemForm Form, IReadOnlyList<CatalogVariantResponse> Variants, IReadOnlyList<string> Decorations)
{
    /// <summary>Maps a catalog object to its API shape.</summary>
    /// <param name="o">The object.</param>
    public static CatalogObjectResponse From(CatalogObject o)
    {
        var variants = (o.Variants ?? []).Select(CatalogVariantResponse.From).ToList();

        return new(o.Key, o.Label, o.Shape, o.Width, o.Depth, o.Height, o.Color.Value, o.Elevation, o.Form, variants, o.Decorations ?? []);
    }
}
