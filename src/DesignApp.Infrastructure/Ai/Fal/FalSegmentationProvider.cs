using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DesignApp.Application.Analysis;
using DesignApp.Domain.Rooms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace DesignApp.Infrastructure.Ai.Fal;

/// <summary>
/// Step 2 of the analysis on Meta's SAM 3, hosted by fal.ai, which charges per request. All objects
/// share one request: every box at once, with no text, and one mask comes back per box. A wall's,
/// floor's or ceiling's box covers much of the photo, and a box alone outlines it in patches, so
/// surfaces join only with the few points Claude put on them; a surface without points gets a
/// request of its own with its label as the text prompt. So does any object whose shared-request
/// mask doesn't fit its box. SAM 3 finds every match of a text, so
/// it can return a look-alike next to the object (the end of the bed for "mint throw blanket") or
/// only part of it; it is asked for several masks, and the one (or the union of pieces) whose
/// outline best fits Claude's box is kept. When nothing fits, the object is asked for again with
/// the box alone. Requests run in parallel up to <see cref="AiOptions.SegmentationParallelism"/>.
/// </summary>
/// <param name="http">Sends the requests; authenticated and given long timeouts at registration.</param>
/// <param name="options">Supplies the endpoint and parallelism.</param>
/// <param name="logger">Logs which mask was picked for each object.</param>
internal sealed partial class FalSegmentationProvider(HttpClient http, IOptions<AiOptions> options, ILogger<FalSegmentationProvider> logger)
    : ISegmentationProvider
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<ObjectMask?>> SegmentAsync(
        AnalysisImage image, IReadOnlyList<InventoryItem> items, CancellationToken cancellationToken)
    {
        var requests = CountRequests();
        var before = requests.Value;
        var imageUrl = Constants.Ai.Fal.JpegDataUri + Convert.ToBase64String(image.Jpeg);
        var masks = new ObjectMask?[items.Count];
        // A surface shares only when it has points; without them its box alone outlines it in patches.
        var shared = Enumerable.Range(0, items.Count)
            .Where(i => !items[i].Category.IsSurface() || items[i].Points is { Count: > 0 })
            .ToList();
        // One object gains nothing from sharing; its own request costs the same and has its label.
        if (shared.Count < Constants.Ai.Fal.MinShared)
            shared.Clear();

        var own = Enumerable.Range(0, items.Count).Except(shared).ToList();
        var sharedTask = SegmentSharedAsync(imageUrl, image, [.. shared.Select(i => items[i])], cancellationToken);
        await SegmentEachAsync(own);

        var sharedMasks = await sharedTask;
        var retry = new List<int>();
        for (var k = 0; k < shared.Count; k++)
        {
            if (sharedMasks[k] is { } mask)
                masks[shared[k]] = mask;
            else
                retry.Add(shared[k]);
        }
        if (shared.Count > 0)
            LogShared(shared.Count, shared.Count - retry.Count);
        await SegmentEachAsync(retry);
        LogRequests(items.Count, requests.Value - before);
        return masks;

        Task SegmentEachAsync(List<int> indexes) => Parallel.ForEachAsync(
            indexes,
            new ParallelOptions { MaxDegreeOfParallelism = options.Value.SegmentationParallelism, CancellationToken = cancellationToken },
            async (i, ct) => masks[i] = await SegmentOneAsync(imageUrl, image, items[i], ct));
    }

    /// <summary>The requests sent in the current analysis; fal.ai charges per request.</summary>
    internal sealed class RequestCount
    {
        private int _value;

        /// <summary>How many requests were sent.</summary>
        public int Value => Volatile.Read(ref _value);

        /// <summary>Counts one more request.</summary>
        public void Add() => Interlocked.Increment(ref _value);
    }

    /// <summary>The count of the analysis running in this async flow, shared with its parallel requests.</summary>
    private static readonly AsyncLocal<RequestCount?> CurrentCount = new();

    /// <summary>
    /// The request count for this async flow, starting one when there is none. A caller that calls
    /// this before <see cref="SegmentAsync"/> reads afterwards how many requests the analysis sent.
    /// </summary>
    internal static RequestCount CountRequests() => CurrentCount.Value ??= new RequestCount();

    /// <summary>
    /// Outlines several objects in one request from their boxes (and surfaces' points), with no
    /// text. A mask is kept only when it
    /// fits its box; a refused or failed request keeps none, so every object gets its own request.
    /// </summary>
    /// <param name="imageUrl">The analysis image as a data URI.</param>
    /// <param name="image">The analysis image, for its size.</param>
    /// <param name="items">The objects; none sends no request.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Per object, in order, its mask or <c>null</c> when it needs its own request.</returns>
    private async Task<ObjectMask?[]> SegmentSharedAsync(
        string imageUrl, AnalysisImage image, IReadOnlyList<InventoryItem> items, CancellationToken cancellationToken)
    {
        var result = new ObjectMask?[items.Count];
        if (items.Count == 0)
            return result;

        List<(ObjectMask Mask, int? Index)> answer;
        try
        {
            // Each box is its own object (its id), and one mask is asked for per box. A surface's
            // points carry its id, so SAM 3 adds them to that box.
            var boxes = items.Select((item, k) => PixelBox(item, image) with { ObjectId = k + 1 }).ToList();
            var points = items
                .SelectMany((item, k) => item.Category.IsSurface() ? (item.Points ?? []).Select(p => PixelPoint(p, image, k + 1)) : [])
                .ToList();
            answer = await RequestWithIndexesAsync(
                new SamRequest(imageUrl, "", boxes, MaxMasks: items.Count, PointPrompts: points.Count > 0 ? points : null),
                cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException)
        {
            LogSharedFailed(items.Count, e.Message);
            return result;
        }

        // A mask's index is the position of the box it outlines.
        foreach (var (mask, index) in answer)
        {
            if (index is not { } k || k < 0 || k >= items.Count || result[k] is not null)
                continue;
            if (Pick([mask], items[k].Box) is { Fit: >= Constants.Ai.Fal.MinFit } picked)
            {
                LogPicked(items[k].Label, picked.Fit, "shared request");
                result[k] = picked.Mask;
            }
        }
        return result;
    }

    /// <summary>Outlines one object: the best-fitting mask of a text-and-box request, else of a box-only one.</summary>
    /// <param name="imageUrl">The analysis image as a data URI.</param>
    /// <param name="image">The analysis image, for its size.</param>
    /// <param name="item">The object to outline.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    /// <returns>The mask, or <c>null</c> when SAM 3 found nothing for it.</returns>
    private async Task<ObjectMask?> SegmentOneAsync(string imageUrl, AnalysisImage image, InventoryItem item, CancellationToken cancellationToken)
    {
        var box = PixelBox(item, image);
        var best = Pick(await RequestAsync(new SamRequest(imageUrl, item.Label, [box]), cancellationToken), item.Box);
        if (best is { Fit: >= Constants.Ai.Fal.MinFit })
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

    /// <summary>Claude's box for an object in the analysis image's pixels.</summary>
    /// <param name="item">The object.</param>
    /// <param name="image">The analysis image.</param>
    private static SamBox PixelBox(InventoryItem item, AnalysisImage image) => new(
        (int)Math.Floor(item.Box.X * image.Width),
        (int)Math.Floor(item.Box.Y * image.Height),
        (int)Math.Ceiling((item.Box.X + item.Box.Width) * image.Width),
        (int)Math.Ceiling((item.Box.Y + item.Box.Height) * image.Height));

    /// <summary>A spot on an object in the analysis image's pixels, marked as part of it.</summary>
    /// <param name="point">The spot, as fractions of the image.</param>
    /// <param name="image">The analysis image.</param>
    /// <param name="objectId">The object it belongs to.</param>
    private static SamPoint PixelPoint(ImagePoint point, AnalysisImage image, int objectId) => new(
        Math.Clamp((int)(point.X * image.Width), 0, image.Width - 1),
        Math.Clamp((int)(point.Y * image.Height), 0, image.Height - 1),
        Label: Constants.Ai.Fal.ForegroundPoint,
        ObjectId: objectId);

    /// <summary>Sends a request and decodes every returned mask.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels it.</param>
    /// <returns>The masks SAM 3 returned, best first by its own score.</returns>
    private async Task<List<ObjectMask>> RequestAsync(SamRequest request, CancellationToken cancellationToken) =>
        [.. (await RequestWithIndexesAsync(request, cancellationToken)).Select(m => m.Mask)];

    /// <summary>Sends a request and decodes every returned mask with the index SAM 3 gave it.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels it.</param>
    /// <returns>The masks, best first, each with the position of the box it answers when SAM 3 says.</returns>
    private async Task<List<(ObjectMask Mask, int? Index)>> RequestWithIndexesAsync(SamRequest request, CancellationToken cancellationToken)
    {
        CountRequests().Add();
        using var response = await http.PostAsJsonAsync(options.Value.SegmentationEndpoint, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SamResponse>(cancellationToken);

        var masks = new List<(ObjectMask, int?)>();
        var returned = result?.Masks ?? [];
        for (var i = 0; i < returned.Count; i++)
        {
            if (returned[i].Url is not { Length: > 0 } url)
                continue;
            var png = url.StartsWith(Constants.Ai.Fal.DataUriScheme, StringComparison.Ordinal)
                ? Convert.FromBase64String(url[(url.IndexOf(',', StringComparison.Ordinal) + 1)..])
                : await http.GetByteArrayAsync(new Uri(url), cancellationToken);
            if (Decode(png) is { } mask)
                masks.Add((mask, result?.Metadata is { } metadata && i < metadata.Count ? metadata[i].Index : null));
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

        var inside = masks.Where(m => ShareInside(m, box) >= Constants.Ai.Fal.MinShareInsideBox).ToList();
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
                if (mask.Coverage[y * mask.Width + x] < Constants.Imaging.CoverageThreshold)
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
                if (mask.Coverage[y * mask.Width + x] < Constants.Imaging.CoverageThreshold)
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
            coverage[i] = (byte)(Math.Max(r, Math.Max(g, b)) * a / Constants.Imaging.FullCoverage);
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

    /// <summary>Logs how many requests an analysis sent, which is what it cost.</summary>
    /// <param name="objects">Objects outlined.</param>
    /// <param name="requests">Requests sent.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "SAM 3 outlined {Objects} objects with {Requests} requests")]
    private partial void LogRequests(int objects, int requests);

    /// <summary>Logs how many objects the shared request outlined.</summary>
    /// <param name="count">Objects in the shared request.</param>
    /// <param name="kept">Objects whose mask fit their box.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "SAM 3 shared request: {Kept} of {Count} objects outlined, the rest get their own request")]
    private partial void LogShared(int count, int kept);

    /// <summary>Logs a failed shared request.</summary>
    /// <param name="count">Objects in it, which now get a request each.</param>
    /// <param name="error">What went wrong.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "SAM 3 shared request for {Count} objects failed ({Error}); each gets its own request")]
    private partial void LogSharedFailed(int count, string error);

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
    /// <param name="PointPrompts">Spots on objects, each tied to its object's box; none when null.</param>
    private sealed record SamRequest(
        [property: JsonPropertyName(Constants.Ai.Fal.Json.ImageUrl)] string ImageUrl,
        [property: JsonPropertyName(Constants.Ai.Fal.Json.Prompt)] string Prompt,
        [property: JsonPropertyName(Constants.Ai.Fal.Json.BoxPrompts)] IReadOnlyList<SamBox> BoxPrompts,
        [property: JsonPropertyName(Constants.Ai.Fal.Json.ApplyMask)] bool ApplyMask = false,
        [property: JsonPropertyName(Constants.Ai.Fal.Json.SyncMode)] bool SyncMode = true,
        [property: JsonPropertyName(Constants.Ai.Fal.Json.OutputFormat)] string OutputFormat = Constants.Ai.Fal.OutputFormat,
        [property: JsonPropertyName(Constants.Ai.Fal.Json.ReturnMultipleMasks)] bool ReturnMultipleMasks = true,
        [property: JsonPropertyName(Constants.Ai.Fal.Json.MaxMasks)] int MaxMasks = Constants.Ai.Fal.MaxMasks,
        [property: JsonPropertyName(Constants.Ai.Fal.Json.PointPrompts), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SamPoint>? PointPrompts = null);

    /// <summary>A point prompt in pixels.</summary>
    /// <param name="X">Across.</param>
    /// <param name="Y">Down.</param>
    /// <param name="Label">1: the spot is part of the object.</param>
    /// <param name="ObjectId">The object (box) it belongs to.</param>
    private sealed record SamPoint(
        [property: JsonPropertyName(Constants.Ai.Fal.Json.X)] int X,
        [property: JsonPropertyName(Constants.Ai.Fal.Json.Y)] int Y,
        [property: JsonPropertyName(Constants.Ai.Fal.Json.Label)] int Label,
        [property: JsonPropertyName(Constants.Ai.Fal.Json.ObjectId)] int ObjectId);

    /// <summary>A box prompt in pixels.</summary>
    /// <param name="XMin">Left edge.</param>
    /// <param name="YMin">Top edge.</param>
    /// <param name="XMax">Right edge.</param>
    /// <param name="YMax">Bottom edge.</param>
    /// <param name="ObjectId">Which object the box belongs to, when one request outlines several.</param>
    private sealed record SamBox(
        [property: JsonPropertyName(Constants.Ai.Fal.Json.XMin)] int XMin,
        [property: JsonPropertyName(Constants.Ai.Fal.Json.YMin)] int YMin,
        [property: JsonPropertyName(Constants.Ai.Fal.Json.XMax)] int XMax,
        [property: JsonPropertyName(Constants.Ai.Fal.Json.YMax)] int YMax,
        [property: JsonPropertyName(Constants.Ai.Fal.Json.ObjectId), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ObjectId = null);

    /// <summary>The part of the SAM 3 response the app uses.</summary>
    /// <param name="Masks">The masks, best first.</param>
    /// <param name="Metadata">Per mask, in the same order, which box it answers.</param>
    private sealed record SamResponse(
        [property: JsonPropertyName(Constants.Ai.Fal.Json.Masks)] List<SamImage>? Masks,
        [property: JsonPropertyName(Constants.Ai.Fal.Json.Metadata)] List<SamMaskInfo>? Metadata = null);

    /// <summary>What SAM 3 says about one returned mask.</summary>
    /// <param name="Index">The position of the box prompt it answers.</param>
    private sealed record SamMaskInfo([property: JsonPropertyName(Constants.Ai.Fal.Json.Index)] int? Index);

    /// <summary>One returned mask image.</summary>
    /// <param name="Url">A data URI (sync mode) or a download URL.</param>
    private sealed record SamImage([property: JsonPropertyName(Constants.Ai.Fal.Json.Url)] string? Url);
}
