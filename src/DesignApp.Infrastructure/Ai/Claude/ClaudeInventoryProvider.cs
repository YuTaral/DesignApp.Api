using System.Diagnostics;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using DesignApp.Application.Analysis;
using DesignApp.Application.Common;
using DesignApp.Infrastructure.Ai.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DesignApp.Infrastructure.Ai.Claude;

/// <summary>
/// Step 1 of the analysis on Claude (Sonnet 5.5 by default): one vision request with structured
/// output, so the answer always matches <see cref="InventoryJson.Schema"/>.
/// </summary>
/// <param name="client">The Anthropic client (it retries rate limits and server errors itself).</param>
/// <param name="options">Supplies the model name and effort.</param>
/// <param name="logger">Records the tokens each call used, which is what it costs.</param>
internal sealed partial class ClaudeInventoryProvider(
    IAnthropicClient client,
    IOptions<AiOptions> options,
    ILogger<ClaudeInventoryProvider> logger) : IObjectInventoryProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>Tells Claude what to list and how; the schema enforces the shape.</summary>
    private const string Instructions = """
        You prepare room photos for an interior-restyling app. The user will tap an object and
        recolor it, so list the surfaces and objects a person would want to restyle: each wall
        separately, the floor, the ceiling, furniture, rugs and other textiles, lamps, large decor,
        and fixtures such as doors, windows, cabinets and counters.

        Rules:
        - Always list every wall that shows wall surface as its own entry ("left wall", "back wall",
          "right wall"), even plain white walls and narrow strips beside windows, and the floor and
          ceiling when visible. A tiled area or backsplash is an extra entry, not a replacement for its wall.
        - A wall is only its own surface (paint, plaster, wallpaper). Cabinets, shelves and appliances
          built against it are separate entries, never part of the wall. The wall's box still spans the
          whole wall from edge to edge, including the parts behind furniture, a TV or cabinets. Don't
          list a wall that is completely hidden behind full-height cabinets.
        - Labels of walls, floors and ceilings name their finish ("light grey painted back wall",
          "light oak floor"), not just "wall" or "floor".
        - Split large built-in units into one entry per section, so each entry is one continuous
          surface: "left tall cabinets", "upper cabinets", "right tall cabinets", "lower cabinets",
          "island". Every section of cabinetry needs an entry, including ones at the edges of the photo.
        - Doors and windows (glass doors included) have the category Opening; cabinet and wardrobe
          doors belong to their cabinet or wardrobe.
        - One entry per physical object. Give similar objects distinct labels ("left armchair", "right armchair").
        - Labels are short, lower-case and concrete ("oak dining table", "jute rug"), usable as a segmentation prompt.
        - Skip things smaller than about 1% of the image, people, pets, and text.
        - The box encloses the object's whole visible extent, from its leftmost to its rightmost and
          its top to its bottom visible pixel, as fractions of the image (0 to 1). Check each box
          against the photo before answering; long objects such as islands and counters often span
          most of the photo's width.
        - Give every wall, floor and ceiling 2 or 3 points spread over its visible surface, each
          clearly on the surface itself: on bare wall, floorboards or tiles, never on furniture, a
          rug, a picture or anything else in front of it. Every other object gets no points.
        - Together the entries should cover nearly the whole photo: every large region of the photo
          belongs to exactly one entry.
        - color is the object's main color as #RRGGBB, ignoring shadows and highlights.
        - At most 30 objects, largest and most important first.
        - Set isRoom to false, with no objects, when the photo is not the interior of a room.
        """;

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<InventoryItem>>> ListObjectsAsync(AnalysisImage image, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var started = Stopwatch.GetTimestamp();
        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model = settings.InventoryModel,
            MaxTokens = 16000,
            System = Instructions,
            OutputConfig = new OutputConfig
            {
                Effort = settings.InventoryEffort switch
                {
                    InventoryEffort.Low => Effort.Low,
                    InventoryEffort.High => Effort.High,
                    _ => Effort.Medium,
                },
                Format = new JsonOutputFormat { Schema = InventoryJson.Schema },
            },
            Messages =
            [
                new UserMessageParam
                {
                    Content = new List<ContentBlockParam>
                    {
                        new ImageBlockParam
                        {
                            Source = new Base64ImageSource { Data = Convert.ToBase64String(image.Jpeg), MediaType = MediaType.ImageJpeg },
                        },
                        new TextBlockParam { Text = "List the objects in this room photo." },
                    },
                },
            ],
        }, cancellationToken);

        var elapsed = Stopwatch.GetElapsedTime(started);
        LogUsage(settings.InventoryModel, settings.InventoryEffort, response.Usage.InputTokens, response.Usage.OutputTokens, elapsed.TotalSeconds);
        await RecordUsageAsync(image, settings, response.Usage, elapsed, cancellationToken);

        if (response.StopReason != StopReason.EndTurn)
            throw new InvalidOperationException($"Claude stopped early ({response.StopReason}) while listing objects.");

        var json = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
        return InventoryJson.Parse(json);
    }

    /// <summary>
    /// When a sample photo is analyzed with <see cref="AiOptions.RecordSamplesTo"/> set, saves the
    /// call's token counts next to its recording as <c>name.claude-usage.json</c>, so models and
    /// effort levels can be compared on the same photos. Development use only.
    /// </summary>
    /// <param name="image">The analyzed photo.</param>
    /// <param name="settings">The AI settings the call used.</param>
    /// <param name="usage">The tokens Claude reported.</param>
    /// <param name="elapsed">How long the call took.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    private static async Task RecordUsageAsync(
        AnalysisImage image, AiOptions settings, Usage usage, TimeSpan elapsed, CancellationToken cancellationToken)
    {
        if (settings.RecordSamplesTo is not { Length: > 0 } folder || SampleRooms.Match(image) is not { } sample)
            return;
        var recorded = new
        {
            name = sample.Name,
            model = settings.InventoryModel,
            effort = settings.InventoryEffort.ToString(),
            inputTokens = usage.InputTokens,
            outputTokens = usage.OutputTokens,
            seconds = Math.Round(elapsed.TotalSeconds, 1),
        };
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, $"{sample.Name}.claude-usage.json"), JsonSerializer.Serialize(recorded, Json), cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Claude {Model} ({Effort} effort) listed the objects with {InputTokens} input and {OutputTokens} output tokens in {Seconds:0.0} s")]
    private partial void LogUsage(string model, InventoryEffort effort, long inputTokens, long outputTokens, double seconds);
}
