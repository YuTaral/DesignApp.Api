using DesignApp.Application.Common;
using DesignApp.Application.Photos.Objects;

namespace DesignApp.Application.Photos;

/// <summary>
/// Turns an uploaded file into the photo the app works with: a validated, upright sRGB JPEG,
/// no larger than <see cref="Constants.Photos.MaxLongEdge"/>, with all metadata (including GPS) removed.
/// </summary>
public interface IPhotoProcessor
{
    /// <summary>Validates and normalizes an uploaded file.</summary>
    /// <param name="upload">The uploaded file's bytes.</param>
    /// <returns>The photo, or a failure saying why the file can't be used.</returns>
    Result<ProcessedPhoto> Process(Stream upload);
}
