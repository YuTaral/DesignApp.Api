namespace DesignApp.Infrastructure.Storage;

/// <summary>Any S3-compatible store: RustFS locally, or AWS S3, Cloudflare R2, Backblaze B2 in the cloud.</summary>
public sealed class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>The store's URL. Leave empty to use AWS S3 in <see cref="Region"/>.</summary>
    public string? ServiceUrl { get; set; }

    public string Region { get; set; } = "us-east-1";
    public string Bucket { get; set; } = "designapp";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
}
