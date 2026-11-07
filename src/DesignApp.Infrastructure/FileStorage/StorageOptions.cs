namespace DesignApp.Infrastructure.FileStorage;

/// <summary>Any S3-compatible store: RustFS locally, or AWS S3, Cloudflare R2, Backblaze B2 in the cloud.</summary>
public sealed class StorageOptions
{
    /// <summary>The store's URL. Leave empty to use AWS S3 in <see cref="Region"/>.</summary>
    public string? ServiceUrl { get; set; }

    /// <summary>The AWS region, or the signing region for other stores.</summary>
    public string Region { get; set; } = Constants.Storage.DefaultRegion;
    /// <summary>The bucket all files go into; created on startup if missing.</summary>
    public string Bucket { get; set; } = Constants.Storage.DefaultBucket;
    /// <summary>The store's access key (user name). Required.</summary>
    public string AccessKey { get; set; } = "";
    /// <summary>The store's secret key (password). Required; keep it in user secrets.</summary>
    public string SecretKey { get; set; } = "";
}
