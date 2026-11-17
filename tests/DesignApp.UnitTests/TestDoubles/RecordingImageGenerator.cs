using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;

namespace DesignApp.UnitTests;

/// <summary>An image model that records each call and answers with the image it was given.</summary>
internal sealed class RecordingImageGenerator : IImageGenerator
{
    /// <summary>The images, prompts, seeds and layouts it was given, in order.</summary>
    public List<(byte[] Image, string Prompt, int? Seed, IReadOnlyList<LayoutBox>? Boxes)> Calls { get; } = [];

    /// <inheritdoc/>
    public Task<Result<byte[]>> GenerateAsync(byte[] image, string prompt, int? seed, IReadOnlyList<LayoutBox>? boxes, CancellationToken cancellationToken)
    {
        Calls.Add((image, prompt, seed, boxes));

        return Task.FromResult<Result<byte[]>>(image);
    }
}
