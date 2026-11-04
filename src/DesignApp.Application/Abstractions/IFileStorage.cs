namespace DesignApp.Application.Abstractions;

/// <summary>Blob storage for photos and, later, masks and thumbnails. Keys are paths like "rooms/{id}/photo.jpg".</summary>
public interface IFileStorage
{
    /// <summary>Stores a file, replacing any file already under <paramref name="key"/>.</summary>
    /// <param name="key">Path of the file in storage.</param>
    /// <param name="content">The file's bytes; the caller keeps ownership of the stream.</param>
    /// <param name="contentType">MIME type to serve the file with, such as "image/jpeg".</param>
    /// <param name="cancellationToken">Cancels the upload.</param>
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>Opens a stored file for reading.</summary>
    /// <param name="key">Path of the file in storage.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The file, or <c>null</c> when nothing is stored under <paramref name="key"/>.</returns>
    Task<StoredFile?> OpenReadAsync(string key, CancellationToken cancellationToken);

    /// <summary>Deletes a file. Deleting a missing file is not an error.</summary>
    /// <param name="key">Path of the file in storage.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

/// <summary>An open stored file. The caller disposes it.</summary>
/// <param name="content">The file's bytes, read as a stream.</param>
/// <param name="contentType">The file's MIME type.</param>
/// <param name="length">Size in bytes, when the store reports it.</param>
public sealed class StoredFile(Stream content, string contentType, long? length) : IAsyncDisposable, IDisposable
{
    /// <summary>The file's bytes, read as a stream.</summary>
    public Stream Content { get; } = content;
    /// <summary>The file's MIME type.</summary>
    public string ContentType { get; } = contentType;
    /// <summary>Size in bytes, or <c>null</c> when unknown.</summary>
    public long? Length { get; } = length;

    /// <summary>Closes the content stream.</summary>
    public ValueTask DisposeAsync() => Content.DisposeAsync();

    /// <summary>Closes the content stream.</summary>
    public void Dispose() => Content.Dispose();
}
