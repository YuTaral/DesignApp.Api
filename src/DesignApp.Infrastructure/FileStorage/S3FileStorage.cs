using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using Microsoft.Extensions.Options;

namespace DesignApp.Infrastructure.FileStorage;

/// <summary><see cref="IFileStorage"/> on any S3-compatible store, in one bucket.</summary>
/// <param name="s3">The S3 client.</param>
/// <param name="options">Supplies the bucket name.</param>
internal sealed class S3FileStorage(IAmazonS3 s3, IOptions<StorageOptions> options) : IFileStorage
{
    private readonly string _bucket = options.Value.Bucket;

    /// <inheritdoc/>
    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<StoredFile?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var response = await s3.GetObjectAsync(_bucket, key, cancellationToken);
            return new StoredFile(response.ResponseStream, response.Headers.ContentType, response.Headers.ContentLength);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        await s3.DeleteObjectAsync(_bucket, key, cancellationToken);
    }

    /// <summary>Creates the bucket on first run.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task EnsureBucketExistsAsync(CancellationToken cancellationToken)
    {
        if (!await AmazonS3Util.DoesS3BucketExistV2Async(s3, _bucket))
            await s3.PutBucketAsync(_bucket, cancellationToken);
    }
}
