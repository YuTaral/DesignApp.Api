namespace DesignApp.Application.Photos;

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

    public const int JpegQuality = 90;
}
