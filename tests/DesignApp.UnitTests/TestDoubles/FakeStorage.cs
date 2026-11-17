using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;


namespace DesignApp.UnitTests;

/// <summary>In-memory file storage.</summary>
internal sealed class FakeStorage : IFileStorage
{
    /// <summary>The stored files by key.</summary>
    public Dictionary<string, (byte[] Content, string ContentType)> Files { get; } = [];

    /// <inheritdoc/>
    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();

        await content.CopyToAsync(buffer, cancellationToken);
        Files[key] = (buffer.ToArray(), contentType);
    }

    /// <inheritdoc/>
    public Task<StoredFile?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        return Task.FromResult(Files.TryGetValue(key, out var file)
            ? new StoredFile(new MemoryStream(file.Content), file.ContentType, file.Content.Length)
            : null);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        Files.Remove(key);

        return Task.CompletedTask;
    }
}
