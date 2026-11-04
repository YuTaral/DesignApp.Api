using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using DesignApp.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace DesignApp.Infrastructure.Storage;

internal sealed class S3FileStorage(IAmazonS3 s3, IOptions<StorageOptions> options) : IFileStorage
{
    private readonly string _bucket = options.Value.Bucket;

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken) =>
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
        }, cancellationToken);

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

    public async Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        await s3.DeleteObjectAsync(_bucket, key, cancellationToken);

    public async Task EnsureBucketExistsAsync(CancellationToken cancellationToken)
    {
        if (!await AmazonS3Util.DoesS3BucketExistV2Async(s3, _bucket))
            await s3.PutBucketAsync(_bucket, cancellationToken);
    }
}
