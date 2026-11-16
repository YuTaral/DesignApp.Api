using DesignApp.Application.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Api.Responses;

/// <summary>One variation of a catalog object, such as the L-shaped corner sofa of the sofa, in the object's color.</summary>
/// <param name="Key">A short id within the object.</param>
/// <param name="Name">What the choice is called, such as "L-shaped".</param>
/// <param name="Label">The object's name in this variation.</param>
/// <param name="Shape">How it is drawn in 3D.</param>
/// <param name="Width">Its width, in centimetres.</param>
/// <param name="Depth">Its depth, in centimetres.</param>
/// <param name="Height">Its height, in centimetres.</param>
/// <param name="Elevation">How high its bottom is above the floor, in centimetres (a window's sill; else 0).</param>
/// <param name="Form">Its outline from above.</param>
public sealed record CatalogVariantResponse(string Key, string Name, string Label, ItemShape Shape, double Width, double Depth, double Height, double Elevation, ItemForm Form)
{
    /// <summary>Maps a variation to its API shape.</summary>
    /// <param name="v">The variation.</param>
    public static CatalogVariantResponse From(CatalogVariant v)
    {
        return new(v.Key, v.Name, v.Label, v.Shape, v.Width, v.Depth, v.Height, v.Elevation, v.Form);
    }
}
