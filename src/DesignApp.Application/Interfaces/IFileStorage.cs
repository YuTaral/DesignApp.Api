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
