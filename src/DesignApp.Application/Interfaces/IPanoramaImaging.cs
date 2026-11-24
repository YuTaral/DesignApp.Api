using DesignApp.Application.Objects;

namespace DesignApp.Application.Interfaces;

/// <summary>Prepares 360-degree photos and cuts ordinary views out of them.</summary>
public interface IPanoramaImaging
{
    /// <summary>Turns a picture from the image model into a stored 360-degree photo of the given size.</summary>
    /// <param name="image">The picture (JPEG or PNG), roughly twice as wide as it is tall.</param>
    /// <param name="width">The photo's width.</param>
    /// <param name="height">The photo's height (half its width).</param>
    /// <returns>The photo as a JPEG, or why the picture can't be used.</returns>
    Result<PanoramaPicture> Prepare(byte[] image, int width, int height);

    /// <summary>Cuts an ordinary view out of a 360-degree photo, as a camera at its spot would see it.</summary>
    /// <param name="panorama">The 360-degree photo (JPEG).</param>
    /// <param name="view">Which way the view looks, and its lens.</param>
    /// <param name="width">The view's width in pixels.</param>
    /// <param name="height">The view's height in pixels.</param>
    /// <returns>The view as a JPEG.</returns>
    byte[] Cut(byte[] panorama, PanoramaView view, int width, int height);
}
