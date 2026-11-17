using DesignApp.Application.Interfaces;

namespace DesignApp.Application.Helpers;

/// <summary>Reading whole files from storage, for the steps that work on a stored photo or label map in memory.</summary>
public static class StoredFiles
{
    /// <summary>A stored file's bytes.</summary>
    /// <param name="storage">The file storage.</param>
    /// <param name="key">Path of the file in storage.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The whole file.</returns>
    /// <exception cref="InvalidOperationException">Nothing is stored under <paramref name="key"/>.</exception>
    public static async Task<byte[]> ReadAllBytesAsync(this IFileStorage storage, string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(storage);
        await using var stored = await storage.OpenReadAsync(key, cancellationToken)
            ?? throw new InvalidOperationException(Constants.Messages.MissingFile(key));
        using var buffer = new MemoryStream();

        await stored.Content.CopyToAsync(buffer, cancellationToken);

        return buffer.ToArray();
    }
}
