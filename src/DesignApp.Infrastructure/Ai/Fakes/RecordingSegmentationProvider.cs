using System.Text.Json;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Infrastructure.Ai.Fal;
using DesignApp.Infrastructure.Photos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DesignApp.Infrastructure.Ai.Fakes;

/// <summary>
/// Wraps the real SAM 3 provider. When a sample room photo is analyzed for real, it saves what
/// Claude and SAM 3 returned as <c>name.sam3.json</c> in <see cref="AiOptions.RecordSamplesTo"/>.
/// Copied into <c>Ai/Fakes/Samples</c>, such a file makes the fakes show real SAM 3 outlines for
/// free. Only on when that setting is given; other photos pass straight through.
/// </summary>
/// <param name="inner">The real segmentation provider.</param>
/// <param name="options">Supplies the folder to record into.</param>
/// <param name="logger">Records where each file was written.</param>
internal sealed partial class RecordingSegmentationProvider(
    ISegmentationProvider inner,
    IOptions<AiOptions> options,
    ILogger<RecordingSegmentationProvider> logger) : ISegmentationProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ObjectMask?>> SegmentAsync(
        AnalysisImage image, IReadOnlyList<InventoryItem> items, CancellationToken cancellationToken)
    {
        var requests = FalSegmentationProvider.CountRequests();
        var before = requests.Value;
        var masks = await inner.SegmentAsync(image, items, cancellationToken);

        if (options.Value.RecordSamplesTo is { Length: > 0 } folder && SampleRooms.Match(image) is { } sample)
        {
            // How many paid SAM 3 requests the photo took, to compare ways of asking.
            Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(
                Path.Combine(folder, sample.Name + Constants.Samples.RequestsSuffix),
                JsonSerializer.Serialize(new { name = sample.Name, objects = items.Count, requests = requests.Value - before }, Json),
                cancellationToken);

            var recorded = new
            {
                name = sample.Name,
                width = image.Width,
                height = image.Height,
                fingerprint = sample.Fingerprint,
                objects = items.Zip(masks)
                    .Where(pair => pair.Second is not null)
                    .Select(pair => new
                    {
                        label = pair.First.Label,
                        category = pair.First.Category.ToString(),
                        shapes = new[] { new { mask = Convert.ToBase64String(MaskPixels.GrayPng(pair.Second!.Coverage, pair.Second.Width, pair.Second.Height)) } },
                    }),
            };

            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, sample.Name + Constants.Samples.RecordedSuffix);

            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(recorded, Json), cancellationToken);
            LogRecorded(sample.Name, path);
        }

        return masks;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Recorded the real analysis of sample {Sample} to {Path}")]
    private partial void LogRecorded(string sample, string path);
}
