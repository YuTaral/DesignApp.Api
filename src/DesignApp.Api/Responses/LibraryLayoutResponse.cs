using DesignApp.Application.Objects;

namespace DesignApp.Api.Responses;

/// <summary>One layout of a library room, as listed.</summary>
/// <param name="Id">The layout's id inside its room, such as <c>2</c>.</param>
/// <param name="PhotoUrl">Where its photo is.</param>
/// <param name="ThumbnailUrl">Where a small copy of the photo is, for lists.</param>
/// <param name="Width">Width of the photo, in pixels.</param>
/// <param name="Height">Height of the photo, in pixels.</param>
/// <param name="ObjectCount">How many objects are outlined in it.</param>
internal sealed record LibraryLayoutResponse(string Id, string PhotoUrl, string ThumbnailUrl, int Width, int Height, int ObjectCount)
{
    /// <summary>The response for a layout.</summary>
    /// <param name="designId">Its design's id.</param>
    /// <param name="roomId">Its room's id.</param>
    /// <param name="layout">The layout.</param>
    public static LibraryLayoutResponse From(string designId, string roomId, LibraryLayout layout)
    {
        return new(
            layout.Id,
            Constants.Routes.LibraryLayoutPhotoUrl(designId, roomId, layout.Id),
            Constants.Routes.LibraryLayoutThumbnailUrl(designId, roomId, layout.Id),
            layout.Width,
            layout.Height,
            layout.Objects.Count);
    }
}
