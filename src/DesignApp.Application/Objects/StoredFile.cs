namespace DesignApp.Application.Objects;

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
    public ValueTask DisposeAsync()
    {
        return Content.DisposeAsync();
    }

    /// <summary>Closes the content stream.</summary>
    public void Dispose()
    {
        Content.Dispose();
    }
}
