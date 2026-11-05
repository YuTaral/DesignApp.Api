using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DesignApp.Application.Analysis;
using DesignApp.Domain.Rooms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace DesignApp.Infrastructure.Ai.Fal;

/// <summary>
/// Step 2 of the analysis on Meta's SAM 3, hosted by fal.ai. Each object gets its own request with
/// its label as the text prompt and Claude's box as a box prompt. SAM 3 finds every match of the
/// text, so it can return a look-alike next to the object (the end of the bed for "mint throw
/// blanket") or only part of it; it is asked for several masks, and the one (or the union of
/// pieces) whose outline best fits Claude's box is kept. When nothing fits, the object is asked
/// for again with the box alone. Requests run in parallel up to <see cref="AiOptions.SegmentationParallelism"/>.
/// </summary>
/// <param name="http">Sends the requests; authenticated and given long timeouts at registration.</param>
/// <param name="options">Supplies the endpoint and parallelism.</param>
/// <param name="logger">Logs which mask was picked for each object.</param>
internal sealed partial class FalSegmentationProvider(HttpClient http, IOptions<AiOptions> options, ILogger<FalSegmentationProvider> logger)
    : ISegmentationProvider
{
    /// <summary>How many masks SAM 3 returns per request.</summary>
    internal const int MaxMasks = 3;

    /// <summary>Below this box fit (see <see cref="Fit"/>) the object is asked for again with its box alone.</summary>
    internal const double MinFit = 0.5;

    /// <summary>A piece joins a union only when at least this share of its pixels lies inside Claude's box.</summary>
    internal const double MinShareInsideBox = 0.8;

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

    /// <summary>Outlines one object: the best-fitting mask of a text-and-box request, else of a box-only one.</summary>
    /// <param name="imageUrl">The analysis image as a data URI.</param>
    /// <param name="image">The analysis image, for its size.</param>
    /// <param name="item">The object to outline.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    /// <returns>The mask, or <c>null</c> when SAM 3 found nothing for it.</returns>
    private async Task<ObjectMask?> SegmentOneAsync(string imageUrl, AnalysisImage image, InventoryItem item, CancellationToken cancellationToken)
    {
        var box = new SamBox(
            (int)Math.Floor(item.Box.X * image.Width),
            (int)Math.Floor(item.Box.Y * image.Height),
            (int)Math.Ceiling((item.Box.X + item.Box.Width) * image.Width),
            (int)Math.Ceiling((item.Box.Y + item.Box.Height) * image.Height));

        var best = Pick(await RequestAsync(new SamRequest(imageUrl, item.Label, [box]), cancellationToken), item.Box);
        if (best is { Fit: >= MinFit })
        {
            LogPicked(item.Label, best.Value.Fit, best.Value.How);
            return best.Value.Mask;
        }

        // Nothing fits the box: ask again with the box alone. Some prompts are refused; then the
        // text answer stands.
        var boxOnly = Pick(await TryRequestAsync(new SamRequest(imageUrl, "", [box]), cancellationToken), item.Box);
        var chosen = boxOnly is { } b && b.Fit > (best?.Fit ?? -1) ? boxOnly : best;
        LogFallback(item.Label, best?.Fit ?? 0, boxOnly?.Fit ?? 0, chosen == boxOnly && boxOnly is not null ? "box only" : "text");
        return chosen?.Mask;
    }

    /// <summary>Sends a request and decodes every returned mask.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels it.</param>
    /// <returns>The masks SAM 3 returned, best first by its own score.</returns>
    private async Task<List<ObjectMask>> RequestAsync(SamRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(options.Value.SegmentationEndpoint, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SamResponse>(cancellationToken);

        var masks = new List<ObjectMask>();
        foreach (var url in (result?.Masks ?? []).Select(m => m.Url).OfType<string>().Where(u => u.Length > 0))
        {
            var png = url.StartsWith("data:", StringComparison.Ordinal)
                ? Convert.FromBase64String(url[(url.IndexOf(',', StringComparison.Ordinal) + 1)..])
                : await http.GetByteArrayAsync(new Uri(url), cancellationToken);
            if (Decode(png) is { } mask)
                masks.Add(mask);
        }
        return masks;
    }

    /// <summary>Like <see cref="RequestAsync"/>, but a refused request (4xx) returns no masks.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels it.</param>
    /// <returns>The masks, or none when the request was refused.</returns>
    private async Task<List<ObjectMask>> TryRequestAsync(SamRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await RequestAsync(request, cancellationToken);
        }
        catch (HttpRequestException e) when (e.StatusCode is { } status && (int)status is >= 400 and < 500)
        {
            LogRefused((int)status);
            return [];
        }
    }

    /// <summary>A candidate outline and how well it fits Claude's box.</summary>
    /// <param name="Mask">The outline.</param>
    /// <param name="Fit">How well its bounding box fits Claude's box (see <see cref="Fit"/>).</param>
    /// <param name="How">Which candidate it is, for the log.</param>
    internal readonly record struct Candidate(ObjectMask Mask, double Fit, string How);

    /// <summary>
    /// Picks the candidate whose outline best fits Claude's box: each mask on its own, and the union
    /// of the masks that lie mostly inside the box (an object SAM 3 split in pieces).
    /// </summary>
    /// <param name="masks">The masks SAM 3 returned.</param>
    /// <param name="box">Claude's box for the object.</param>
    /// <returns>The best candidate, or <c>null</c> when there are no non-empty masks.</returns>
    internal static Candidate? Pick(IReadOnlyList<ObjectMask> masks, BoundingBox box)
    {
        Candidate? best = null;
        void Consider(ObjectMask mask, string how)
        {
            if (Bounds(mask) is { } bounds && Fit(bounds, box) is var fit && (best is null || fit > best.Value.Fit))
                best = new Candidate(mask, fit, how);
        }

        for (var i = 0; i < masks.Count; i++)
            Consider(masks[i], $"mask {i + 1} of {masks.Count}");

        var inside = masks.Where(m => ShareInside(m, box) >= MinShareInsideBox).ToList();
        if (inside.Count > 1)
            Consider(Union(inside), $"{inside.Count} pieces joined");
        return best;
    }

    /// <summary>The bounding box of a mask's pixels, normalized; <c>null</c> when it has none.</summary>
    /// <param name="mask">The mask.</param>
    internal static (double X0, double Y0, double X1, double Y1)? Bounds(ObjectMask mask)
    {
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
        for (var y = 0; y < mask.Height; y++)
        {
            for (var x = 0; x < mask.Width; x++)
            {
                if (mask.Coverage[y * mask.Width + x] < 128)
                    continue;
                (x0, y0, x1, y1) = (Math.Min(x0, x), Math.Min(y0, y), Math.Max(x1, x), Math.Max(y1, y));
            }
        }
        return x1 < 0 ? null : ((double)x0 / mask.Width, (double)y0 / mask.Height, (x1 + 1.0) / mask.Width, (y1 + 1.0) / mask.Height);
    }

    /// <summary>
    /// How well a mask's box fits Claude's box, from 0 to 1: the F2 score of the overlap, which
    /// counts missing part of Claude's box four times as much as spilling past it. Claude's boxes
    /// are often a little tight (a cabinet run cut short), so a mask that covers the whole object
    /// and a bit more must beat one that covers only part of it; a look-alike elsewhere covers
    /// little of the box and scores low either way.
    /// </summary>
    /// <param name="a">A mask's box as edges.</param>
    /// <param name="b">Claude's box.</param>
    internal static double Fit((double X0, double Y0, double X1, double Y1) a, BoundingBox b)
    {
        var (bx1, by1) = (b.X + b.Width, b.Y + b.Height);
        var w = Math.Min(a.X1, bx1) - Math.Max(a.X0, b.X);
        var h = Math.Min(a.Y1, by1) - Math.Max(a.Y0, b.Y);
        if (w <= 0 || h <= 0)
            return 0;
        var intersection = w * h;
        var recall = intersection / (b.Width * b.Height);
        var precision = intersection / ((a.X1 - a.X0) * (a.Y1 - a.Y0));
        return 5 * precision * recall / (4 * precision + recall);
    }

    /// <summary>The share of a mask's pixels that lie inside a box.</summary>
    /// <param name="mask">The mask.</param>
    /// <param name="box">The box.</param>
    private static double ShareInside(ObjectMask mask, BoundingBox box)
    {
        int all = 0, inside = 0;
        for (var y = 0; y < mask.Height; y++)
        {
            for (var x = 0; x < mask.Width; x++)
            {
                if (mask.Coverage[y * mask.Width + x] < 128)
                    continue;
                all++;
                var (nx, ny) = ((x + 0.5) / mask.Width, (y + 0.5) / mask.Height);
                if (nx >= box.X && nx <= box.X + box.Width && ny >= box.Y && ny <= box.Y + box.Height)
                    inside++;
            }
        }
        return all == 0 ? 0 : (double)inside / all;
    }

    /// <summary>The union of masks of the same size (others are skipped).</summary>
    /// <param name="masks">The masks; the first sets the size.</param>
    private static ObjectMask Union(List<ObjectMask> masks)
    {
        var first = masks[0];
        var coverage = (byte[])first.Coverage.Clone();
        foreach (var mask in masks.Skip(1).Where(m => m.Width == first.Width && m.Height == first.Height))
        {
            for (var i = 0; i < coverage.Length; i++)
                coverage[i] = Math.Max(coverage[i], mask.Coverage[i]);
        }
        return new ObjectMask(first.Width, first.Height, coverage);
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

    /// <summary>Logs the mask picked for an object.</summary>
    /// <param name="label">The object.</param>
    /// <param name="fit">How well it fits Claude's box.</param>
    /// <param name="how">Which candidate it was.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "SAM 3 {Label}: fit {Fit:0.00} ({How})")]
    private partial void LogPicked(string label, double fit, string how);

    /// <summary>Logs a box-only retry.</summary>
    /// <param name="label">The object.</param>
    /// <param name="textFit">The best fit of the text request.</param>
    /// <param name="boxFit">The best fit of the box-only request.</param>
    /// <param name="kept">Which answer was kept.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "SAM 3 {Label}: poor fit {TextFit:0.00}, box only {BoxFit:0.00}, kept {Kept}")]
    private partial void LogFallback(string label, double textFit, double boxFit, string kept);

    /// <summary>Logs a refused box-only request.</summary>
    /// <param name="status">The HTTP status.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "SAM 3 refused a box-only request ({Status})")]
    private partial void LogRefused(int status);

    /// <summary>The SAM 3 request body.</summary>
    /// <param name="ImageUrl">The image, as a URL or data URI.</param>
    /// <param name="Prompt">What to outline; empty for a box-only request.</param>
    /// <param name="BoxPrompts">Where it is, in pixels.</param>
    /// <param name="ApplyMask">False: return the bare mask, not the mask drawn over the photo.</param>
    /// <param name="SyncMode">True: return the masks inline as data URIs, saving a download per mask.</param>
    /// <param name="OutputFormat">PNG keeps the mask edges exact.</param>
    /// <param name="ReturnMultipleMasks">True: return up to <paramref name="MaxMasks"/> masks to pick from.</param>
    /// <param name="MaxMasks">How many masks to return.</param>
    private sealed record SamRequest(
        [property: JsonPropertyName("image_url")] string ImageUrl,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("box_prompts")] IReadOnlyList<SamBox> BoxPrompts,
        [property: JsonPropertyName("apply_mask")] bool ApplyMask = false,
        [property: JsonPropertyName("sync_mode")] bool SyncMode = true,
        [property: JsonPropertyName("output_format")] string OutputFormat = "png",
        [property: JsonPropertyName("return_multiple_masks")] bool ReturnMultipleMasks = true,
        [property: JsonPropertyName("max_masks")] int MaxMasks = MaxMasks);

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
