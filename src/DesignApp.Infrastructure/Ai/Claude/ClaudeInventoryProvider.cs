using Anthropic;
using Anthropic.Models.Messages;
using DesignApp.Application.Analysis;
using DesignApp.Application.Common;
using Microsoft.Extensions.Options;

namespace DesignApp.Infrastructure.Ai.Claude;

/// <summary>
/// Step 1 of the analysis on Claude (Haiku 5.5 by default): one vision request with structured
/// output, so the answer always matches <see cref="InventoryJson.Schema"/>.
/// </summary>
/// <param name="client">The Anthropic client (it retries rate limits and server errors itself).</param>
/// <param name="options">Supplies the model name.</param>
internal sealed class ClaudeInventoryProvider(IAnthropicClient client, IOptions<AiOptions> options) : IObjectInventoryProvider
{
    /// <summary>Tells Claude what to list and how; the schema enforces the shape.</summary>
    private const string Instructions = """
        You prepare room photos for an interior-restyling app. The user will tap an object and
        recolor it, so list the surfaces and objects a person would want to restyle: each wall
        separately, the floor, the ceiling, furniture, rugs and other textiles, lamps, large decor,
        and fixtures such as doors, windows, cabinets and counters.

        Rules:
        - One entry per physical object. Give similar objects distinct labels ("left armchair", "right armchair").
        - Labels are short, lower-case and concrete ("oak dining table", "jute rug"), usable as a segmentation prompt.
        - Skip things smaller than about 1% of the image, people, pets, and text.
        - The box must tightly enclose the visible part of the object, as fractions of the image (0 to 1).
        - color is the object's main color as #RRGGBB, ignoring shadows and highlights.
        - At most 30 objects, largest and most important first.
        - Set isRoom to false, with no objects, when the photo is not the interior of a room.
        """;

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<InventoryItem>>> ListObjectsAsync(AnalysisImage image, CancellationToken cancellationToken)
    {
        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model = options.Value.InventoryModel,
            MaxTokens = 16000,
            System = Instructions,
            OutputConfig = new OutputConfig
            {
                Effort = Effort.Medium,
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

        if (response.StopReason != StopReason.EndTurn)
            throw new InvalidOperationException($"Claude stopped early ({response.StopReason}) while listing objects.");

        var json = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
        return InventoryJson.Parse(json);
    }
}
