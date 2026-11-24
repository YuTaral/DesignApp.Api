using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;

namespace DesignApp.UnitTests;

/// <summary>An image model that records each call and answers with the image it was given.</summary>
internal sealed class RecordingImageGenerator : IImageGenerator
{
    /// <summary>The images, prompts, seeds and layouts it was given, in order.</summary>
    public List<(byte[] Image, string Prompt, int? Seed, IReadOnlyList<LayoutBox>? Boxes)> Calls { get; } = [];

    /// <summary>The reference photos it was given, in the same order (<c>null</c> for none).</summary>
    public List<byte[]?> References { get; } = [];

    /// <summary>The sizes it was asked for, in the same order (<c>null</c> for a builder photo's).</summary>
    public List<PhotoSize?> Sizes { get; } = [];

    /// <inheritdoc/>
    public Task<Result<byte[]>> GenerateAsync(
        byte[] image, byte[]? reference, string prompt, int? seed, IReadOnlyList<LayoutBox>? boxes, PhotoSize? size, CancellationToken cancellationToken)
    {
        Calls.Add((image, prompt, seed, boxes));
        References.Add(reference);
        Sizes.Add(size);

        return Task.FromResult<Result<byte[]>>(image);
    }
}
