using DesignApp.Application.Common;

namespace DesignApp.Application.Photos;

/// <summary>
/// Turns an uploaded file into the photo the app works with: a validated, upright sRGB JPEG,
/// no larger than <see cref="PhotoRules.MaxLongEdge"/>, with all metadata (including GPS) removed.
/// </summary>
public interface IPhotoProcessor
{
    /// <returns>The photo, or a failure saying why the file can't be used.</returns>
    Result<ProcessedPhoto> Process(Stream upload);
}

public sealed record ProcessedPhoto(byte[] Content, int Width, int Height)
{
    public const string ContentType = "image/jpeg";
}
