using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DesignApp.Application.Analysis;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace DesignApp.Infrastructure.Ai.Fal;

/// <summary>
/// Step 2 of the analysis on Meta's SAM 3, hosted by fal.ai. Each object gets its own request with
/// its label as the text prompt and Claude's box as a box prompt, so look-alikes (two armchairs)
/// are told apart. Requests run in parallel up to <see cref="AiOptions.SegmentationParallelism"/>.
/// </summary>
/// <param name="http">Sends the requests; authenticated and given long timeouts at registration.</param>
/// <param name="options">Supplies the endpoint and parallelism.</param>
internal sealed class FalSegmentationProvider(HttpClient http, IOptions<AiOptions> options) : ISegmentationProvider
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<ObjectMask?>> SegmentAsync(
        AnalysisImage image, IReadOnlyList<InventoryItem> items, CancellationToken cancellationToken)
    {
        var imageUrl = "data:image/jpeg;base64," + Convert.ToBase64String(image.Jpeg);
        var masks = new ObjectMask?[items.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, items.Count),
            new ParallelOptions { MaxDegreeOfParallelism = options.Value.SegmentationParallelism, CancellationToken = cancellationToken },
            async (i, ct) => masks[i] = await SegmentOneAsync(imageUrl, image, items[i], ct));
        return masks;
    }

    /// <summary>Outlines one object.</summary>
    /// <param name="imageUrl">The analysis image as a data URI.</param>
    /// <param name="image">The analysis image, for its size.</param>
    /// <param name="item">The object to outline.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The mask, or <c>null</c> when SAM 3 found nothing for it.</returns>
    private async Task<ObjectMask?> SegmentOneAsync(string imageUrl, AnalysisImage image, InventoryItem item, CancellationToken cancellationToken)
    {
        var request = new SamRequest(
            imageUrl,
            item.Label,
            [new SamBox(
                (int)Math.Floor(item.Box.X * image.Width),
                (int)Math.Floor(item.Box.Y * image.Height),
                (int)Math.Ceiling((item.Box.X + item.Box.Width) * image.Width),
                (int)Math.Ceiling((item.Box.Y + item.Box.Height) * image.Height))]);

        using var response = await http.PostAsJsonAsync(options.Value.SegmentationEndpoint, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SamResponse>(cancellationToken);
        var url = result?.Masks?.FirstOrDefault()?.Url;
        if (string.IsNullOrEmpty(url))
            return null;

        var png = url.StartsWith("data:", StringComparison.Ordinal)
            ? Convert.FromBase64String(url[(url.IndexOf(',', StringComparison.Ordinal) + 1)..])
            : await http.GetByteArrayAsync(new Uri(url), cancellationToken);
        return Decode(png);
    }

    /// <summary>Reads a mask image: bright, opaque pixels belong to the object.</summary>
    /// <param name="png">The mask image file.</param>
    /// <returns>The mask, or <c>null</c> when the image can't be read.</returns>
    internal static ObjectMask? Decode(byte[] png)
    {
        using var codec = SKCodec.Create(new MemoryStream(png));
        if (codec is null)
            return null;
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = SKBitmap.Decode(codec, info);
        if (bitmap is null)
            return null;

        var pixels = bitmap.GetPixelSpan();
        var coverage = new byte[info.Width * info.Height];
        for (var i = 0; i < coverage.Length; i++)
        {
            var (r, g, b, a) = (pixels[i * 4], pixels[i * 4 + 1], pixels[i * 4 + 2], pixels[i * 4 + 3]);
            coverage[i] = (byte)(Math.Max(r, Math.Max(g, b)) * a / 255);
        }
        return new ObjectMask(info.Width, info.Height, coverage);
    }

    /// <summary>The SAM 3 request body.</summary>
    /// <param name="ImageUrl">The image, as a URL or data URI.</param>
    /// <param name="Prompt">What to outline.</param>
    /// <param name="BoxPrompts">Where it is, in pixels.</param>
    /// <param name="ApplyMask">False: return the bare mask, not the mask drawn over the photo.</param>
    /// <param name="SyncMode">True: return the masks inline as data URIs, saving a download per mask.</param>
    /// <param name="OutputFormat">PNG keeps the mask edges exact.</param>
    private sealed record SamRequest(
        [property: JsonPropertyName("image_url")] string ImageUrl,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("box_prompts")] IReadOnlyList<SamBox> BoxPrompts,
        [property: JsonPropertyName("apply_mask")] bool ApplyMask = false,
        [property: JsonPropertyName("sync_mode")] bool SyncMode = true,
        [property: JsonPropertyName("output_format")] string OutputFormat = "png");

    /// <summary>A box prompt in pixels.</summary>
    /// <param name="XMin">Left edge.</param>
    /// <param name="YMin">Top edge.</param>
    /// <param name="XMax">Right edge.</param>
    /// <param name="YMax">Bottom edge.</param>
    private sealed record SamBox(
        [property: JsonPropertyName("x_min")] int XMin,
        [property: JsonPropertyName("y_min")] int YMin,
        [property: JsonPropertyName("x_max")] int XMax,
        [property: JsonPropertyName("y_max")] int YMax);

    /// <summary>The part of the SAM 3 response the app uses.</summary>
    /// <param name="Masks">The masks, best first.</param>
    private sealed record SamResponse([property: JsonPropertyName("masks")] List<SamImage>? Masks);

    /// <summary>One returned mask image.</summary>
    /// <param name="Url">A data URI (sync mode) or a download URL.</param>
    private sealed record SamImage([property: JsonPropertyName("url")] string? Url);
}
