using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using Microsoft.Extensions.Options;

namespace DesignApp.Infrastructure.Ai.Fakes;

/// <summary>
/// The free stand-in for the image model: the "photo" is the image it was given (the room's 3D view, or the photo to
/// decorate), so the whole flow (the new room,
/// its analysis, the editor) runs in development without paying for an image.
/// </summary>
/// <param name="options">The fake step delay.</param>
internal sealed class FakeImageGenerator(IOptions<AiOptions> options) : IImageGenerator
{
    /// <inheritdoc/>
    public async Task<Result<byte[]>> GenerateAsync(
        byte[] image, byte[]? reference, string prompt, int? seed, IReadOnlyList<LayoutBox>? boxes, PhotoSize? size, CancellationToken cancellationToken)
    {
        await Task.Delay(options.Value.FakeStepDelay, cancellationToken);

        return image;
    }
}
