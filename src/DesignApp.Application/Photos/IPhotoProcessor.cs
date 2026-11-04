namespace DesignApp.Application.Photos;

/// <summary>
/// Turns an uploaded file into the photo the app works with: a validated, upright sRGB JPEG,
/// no larger than <see cref="PhotoRules.MaxLongEdge"/>, with all metadata (including GPS) removed.
/// </summary>
public interface IPhotoProcessor
{
    /// <exception cref="InvalidPhotoException">The file is not a usable photo.</exception>
    ProcessedPhoto Process(Stream upload);
}

public sealed record ProcessedPhoto(byte[] Content, int Width, int Height)
{
    public const string ContentType = "image/jpeg";
}
