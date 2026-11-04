using DesignApp.Application.Common;

namespace DesignApp.Application.Photos;

/// <summary>
/// Turns an uploaded file into the photo the app works with: a validated, upright sRGB JPEG,
/// no larger than <see cref="PhotoRules.MaxLongEdge"/>, with all metadata (including GPS) removed.
/// </summary>
public interface IPhotoProcessor
{
    /// <summary>Validates and normalizes an uploaded file.</summary>
    /// <param name="upload">The uploaded file's bytes.</param>
    /// <returns>The photo, or a failure saying why the file can't be used.</returns>
    Result<ProcessedPhoto> Process(Stream upload);
}

/// <summary>A photo ready to store: a JPEG plus its size.</summary>
/// <param name="Content">The encoded JPEG bytes.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
public sealed record ProcessedPhoto(byte[] Content, int Width, int Height)
{
    /// <summary>The MIME type of <see cref="Content"/>; processed photos are always JPEG.</summary>
    public const string ContentType = "image/jpeg";
}
