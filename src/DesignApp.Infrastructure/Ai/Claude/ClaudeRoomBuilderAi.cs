using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic;
using Anthropic.Models.Messages;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Extensions;
using DesignApp.Domain.Objects;
using Microsoft.Extensions.Logging;

using static DesignApp.Domain.Constants;
using F = DesignApp.Infrastructure.Constants.Ai.RoomBuilder.Fields;
using RB = DesignApp.Infrastructure.Constants.Ai.RoomBuilder;

namespace DesignApp.Infrastructure.Ai.Claude;

/// <summary>
/// The room builder's language model on Claude Haiku: a described object comes back in a fixed JSON schema (structured
/// output). The photo prompt is written from the plan itself (<c>PlanPrompt</c>), with its exact sizes.
/// </summary>
/// <param name="client">The Anthropic client.</param>
/// <param name="logger">Records the tokens each call used.</param>
internal sealed partial class ClaudeRoomBuilderAi(IAnthropicClient client, ILogger<ClaudeRoomBuilderAi> logger) : IRoomBuilderAi
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <inheritdoc/>
    public async Task<Result<CatalogObject>> DescribeObjectAsync(RoomType roomType, string description, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model = RB.Model,
            MaxTokens = RB.MaxTokens,
            System = RB.DescribeSystem,
            OutputConfig = new OutputConfig { Format = new JsonOutputFormat { Schema = Schema() } },
            Messages = [User(RB.DescribeMessage(Words(roomType.ToString()), description))],
        }, cancellationToken);
        var seconds = Stopwatch.GetElapsedTime(started).TotalSeconds;

        LogUsage("described an object", response.Usage.InputTokens, response.Usage.OutputTokens, seconds);

        return response.StopReason == StopReason.EndTurn ? ReadObject(Text(response)) : Result.Failure<CatalogObject>(RB.Messages.DescribeFailed);
    }

    /// <summary>Reads a described object from Claude's JSON answer (the API and the Claude Code route share it).</summary>
    /// <param name="json">The answer.</param>
    /// <returns>The object, kept within the limits, or a failure the user can read.</returns>
    internal static Result<CatalogObject> ReadObject(string json)
    {
        try
        {
            var answer = JsonSerializer.Deserialize<Described>(json, Json);

            if (answer is null || string.IsNullOrWhiteSpace(answer.Label) || !Enum.IsDefined(answer.Shape))
            {
                return Result.Failure<CatalogObject>(RB.Messages.DescribeFailed);
            }

            var color = HexColor.TryParse(answer.Color, out var parsed) ? parsed : HexColor.Parse(Domain.Constants.Colors.Fallback);
            // Something hung on a wall sticks out only so far.
            var depth = answer.Shape.IsMounted() ? Math.Min(Side(answer.Depth), Domain.Constants.Plans.MaxMountedDepth) : Side(answer.Depth);

            return new CatalogObject(RB.CustomKey, Trim(answer.Label), answer.Shape, Side(answer.Width), depth, Side(answer.Height), color, answer.Shape.UsualElevation());
        }
        catch (JsonException)
        {
            return Result.Failure<CatalogObject>(RB.Messages.DescribeFailed);
        }
    }

    /// <summary>A user message of one text.</summary>
    /// <param name="text">The text.</param>
    private static UserMessageParam User(string text)
    {
        return new UserMessageParam { Content = new List<ContentBlockParam> { new TextBlockParam { Text = text } } };
    }

    /// <summary>The JSON schema of a described object.</summary>
    internal static Dictionary<string, JsonElement> Schema()
    {
        return new()
        {
            ["type"] = JsonSerializer.SerializeToElement("object"),
            ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
            ["required"] = JsonSerializer.SerializeToElement(new[] { F.Label, F.Shape, F.Width, F.Depth, F.Height, F.Color }),
            ["properties"] = JsonSerializer.SerializeToElement(new Dictionary<string, object>
            {
                [F.Label] = new { type = "string" },
                [F.Shape] = new { type = "string", @enum = Enum.GetNames<ItemShape>() },
                [F.Width] = new { type = "number" },
                [F.Depth] = new { type = "number" },
                [F.Height] = new { type = "number" },
                [F.Color] = new { type = "string" },
            }),
        };
    }

    /// <summary>The text of an answer.</summary>
    /// <param name="response">The answer.</param>
    private static string Text(Message response)
    {
        return string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
    }

    /// <summary>A side kept in the allowed range.</summary>
    /// <param name="centimetres">The side Claude gave.</param>
    private static double Side(double centimetres)
    {
        return double.IsFinite(centimetres)
            ? Math.Clamp(Math.Round(centimetres), Domain.Constants.Plans.MinItemCentimetres, Domain.Constants.Plans.MaxItemCentimetres)
            : Domain.Constants.Plans.MinItemCentimetres;
    }

    /// <summary>A name kept to the allowed length.</summary>
    /// <param name="label">The name Claude gave.</param>
    private static string Trim(string label)
    {
        var trimmed = label.Trim();

        return trimmed.Length <= Domain.Constants.Plans.MaxLabelLength ? trimmed : trimmed[..Domain.Constants.Plans.MaxLabelLength];
    }

    /// <summary>A PascalCase name as words, such as "living room".</summary>
    /// <param name="name">The name.</param>
    internal static string Words(string name)
    {
        return string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
    }

    /// <summary>A described object as Claude answers it.</summary>
    /// <param name="Label">Its name.</param>
    /// <param name="Shape">Its shape.</param>
    /// <param name="Width">Its width, in centimetres.</param>
    /// <param name="Depth">Its depth, in centimetres.</param>
    /// <param name="Height">Its height, in centimetres.</param>
    /// <param name="Color">Its color.</param>
    private sealed record Described(string? Label, ItemShape Shape, double Width, double Depth, double Height, string? Color);

    [LoggerMessage(Level = LogLevel.Information, Message = "Claude Haiku {Task} with {InputTokens} input and {OutputTokens} output tokens in {Seconds:0.0} s")]
    private partial void LogUsage(string task, long inputTokens, long outputTokens, double seconds);
}
