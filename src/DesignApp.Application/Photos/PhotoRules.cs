namespace DesignApp.Application.Photos;

/// <summary>Limits that every uploaded photo must meet.</summary>
public static class PhotoRules
{
    /// <summary>Matches the frontend's limit. Large enough for any phone photo.</summary>
    public const long MaxUploadBytes = 15 * 1024 * 1024;

    /// <summary>Below this the AI cannot find objects reliably.</summary>
    public const int MinShortEdge = 256;

    /// <summary>Larger photos are scaled down: more pixels cost more to analyze without improving results.</summary>
    public const int MaxLongEdge = 4096;

    /// <summary>Refuses huge images before decoding them, so a small file cannot exhaust memory.</summary>
    public const long MaxSourcePixels = 100_000_000;

    /// <summary>JPEG quality (0-100) for stored photos: visually lossless at a reasonable size.</summary>
    public const int JpegQuality = 90;
}
