using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Api.Responses;

/// <summary>One detected object as the API returns it.</summary>
/// <param name="Index">The object's value in the label map.</param>
/// <param name="Label">What the object is, such as "sofa".</param>
/// <param name="Category">The kind of object, such as Wall or Furniture.</param>
/// <param name="Box">Where the object is, normalized to 0..1 of the photo's width and height.</param>
/// <param name="DominantColor">The object's main color as "#RRGGBB".</param>
public sealed record DetectedObjectResponse(int Index, string Label, ObjectCategory Category, BoundingBoxResponse Box, string DominantColor)
{
    /// <summary>Maps a domain object to its API shape.</summary>
    /// <param name="o">The object to map.</param>
    public static DetectedObjectResponse From(DetectedObject o) =>
        new(o.Index, o.Label, o.Category, new(o.Box.X, o.Box.Y, o.Box.Width, o.Box.Height), o.DominantColor.Value);
}
