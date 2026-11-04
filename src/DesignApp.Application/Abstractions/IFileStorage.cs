namespace DesignApp.Application.Abstractions;

/// <summary>Blob storage for photos and, later, masks and thumbnails. Keys are paths like "rooms/{id}/photo.jpg".</summary>
public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <returns>The file, or <c>null</c> when nothing is stored under <paramref name="key"/>.</returns>
    Task<StoredFile?> OpenReadAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

/// <summary>An open stored file. The caller disposes it.</summary>
public sealed class StoredFile(Stream content, string contentType, long? length) : IAsyncDisposable, IDisposable
{
    public Stream Content { get; } = content;
    public string ContentType { get; } = contentType;
    public long? Length { get; } = length;

    public ValueTask DisposeAsync() => Content.DisposeAsync();

    public void Dispose() => Content.Dispose();
}
