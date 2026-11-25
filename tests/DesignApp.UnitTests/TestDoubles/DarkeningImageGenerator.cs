using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using SkiaSharp;

namespace DesignApp.UnitTests;

/// <summary>An image model that answers with the image it was given, darker, as the real one darkens light colors.</summary>
internal sealed class DarkeningImageGenerator : IImageGenerator
{
    /// <summary>The share of each channel it keeps.</summary>
    private const float Keep = 0.6f;

    /// <inheritdoc/>
    public Task<Result<byte[]>> GenerateAsync(
        byte[] image, byte[]? reference, string prompt, int? seed, IReadOnlyList<LayoutBox>? boxes, PhotoSize? size, CancellationToken cancellationToken)
    {
        using var bitmap = SKBitmap.Decode(image);

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var color = bitmap.GetPixel(x, y);

                bitmap.SetPixel(x, y, new SKColor((byte)(color.Red * Keep), (byte)(color.Green * Keep), (byte)(color.Blue * Keep)));
            }
        }

        using var darker = SKImage.FromBitmap(bitmap);
        using var data = darker.Encode(SKEncodedImageFormat.Png, 100);

        return Task.FromResult<Result<byte[]>>(data.ToArray());
    }
}
