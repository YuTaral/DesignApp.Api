using DesignApp.Application.Objects;

namespace DesignApp.Application.Interfaces;

/// <summary>Removes an object from a room photo: fills the masked pixels with a believable background.</summary>
public interface IObjectRemovalProvider
{
    /// <summary>Fills the masked area of the photo.</summary>
    /// <param name="photoJpeg">The photo the object is in (JPEG).</param>
    /// <param name="maskPng">A black-and-white mask the size of the photo: white marks what to remove.</param>
    /// <param name="brushed">Whether the mask was brushed by the user (an object marked by hand) rather than made by the AI.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new photo (any image format), or a failure the user can read.</returns>
    Task<Result<byte[]>> RemoveAsync(byte[] photoJpeg, byte[] maskPng, bool brushed, CancellationToken cancellationToken);
}
