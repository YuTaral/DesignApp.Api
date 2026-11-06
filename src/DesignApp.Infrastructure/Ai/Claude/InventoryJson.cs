using System.Text.Json;
using System.Text.Json.Serialization;
using DesignApp.Application.Analysis;
using DesignApp.Application.Common;
using DesignApp.Domain.Common;
using DesignApp.Domain.Rooms;

using D = DesignApp.Infrastructure.Constants.Ai.Claude.Descriptions;
using F = DesignApp.Infrastructure.Constants.Ai.Claude.Fields;

namespace DesignApp.Infrastructure.Ai.Claude;

/// <summary>
/// The JSON contract between Claude and the app: the schema Claude must answer with, and the code
/// that turns its answer into inventory items. Kept apart from the API call so it can be tested.
/// </summary>
internal static class InventoryJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The JSON schema for Claude's structured output.</summary>
    public static Dictionary<string, JsonElement> Schema { get; } = new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
        ["required"] = JsonSerializer.SerializeToElement(new[] { F.IsRoom, F.Objects }),
        ["properties"] = JsonSerializer.SerializeToElement(new Dictionary<string, object>
        {
            [F.IsRoom] = new { type = "boolean", description = D.IsRoom },
            [F.Objects] = new
            {
                type = "array",
                items = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new[] { F.Label, F.Category, F.Box, F.Color, F.Points },
                    ["properties"] = new Dictionary<string, object>
                    {
                        [F.Label] = new { type = "string", description = D.Label },
                        [F.Category] = new { type = "string", @enum = Enum.GetNames<ObjectCategory>() },
                        [F.Box] = new Dictionary<string, object>
                        {
                            ["type"] = "object",
                            ["additionalProperties"] = false,
                            ["required"] = new[] { F.X, F.Y, F.Width, F.Height },
                            ["description"] = D.Box,
                            ["properties"] = new Dictionary<string, object>
                            {
                                [F.X] = new { type = "number" },
                                [F.Y] = new { type = "number" },
                                [F.Width] = new { type = "number" },
                                [F.Height] = new { type = "number" },
                            },
                        },
                        [F.Color] = new { type = "string", description = D.Color },
                        [F.Points] = new Dictionary<string, object>
                        {
                            ["type"] = "array",
                            ["description"] = D.Points,
                            ["items"] = new Dictionary<string, object>
                            {
                                ["type"] = "object",
                                ["additionalProperties"] = false,
                                ["required"] = new[] { F.X, F.Y },
                                ["properties"] = new Dictionary<string, object> { [F.X] = new { type = "number" }, [F.Y] = new { type = "number" } },
                            },
                        },
                    },
                },
            },
        }),
    };

    /// <summary>Reads Claude's answer.</summary>
    /// <param name="json">The JSON text Claude returned.</param>
    /// <returns>
    /// The usable items (invalid ones are skipped and boxes are clipped to the image), or a
    /// failure when Claude says the photo isn't of a room.
    /// </returns>
    /// <exception cref="JsonException">The text isn't JSON in the expected shape.</exception>
    public static Result<IReadOnlyList<InventoryItem>> Parse(string json)
    {
        var answer = JsonSerializer.Deserialize<Answer>(json, Options)
            ?? throw new JsonException("Claude returned an empty answer.");
        if (!answer.IsRoom)
            return Result.Failure<IReadOnlyList<InventoryItem>>(Constants.Messages.NotARoom);

        var items = new List<InventoryItem>();
        foreach (var o in answer.Objects ?? [])
        {
            if (string.IsNullOrWhiteSpace(o.Label) || o.Box is null || ToBox(o.Box) is not { } box)
                continue;
            var color = HexColor.TryParse(o.Color, out var parsed) ? parsed : HexColor.Parse(Domain.Constants.Colors.Fallback);
            items.Add(new InventoryItem(o.Label.Trim(), o.Category, box, color, ToPoints(o.Points)));
        }
        return items;
    }

    /// <summary>The points inside the image, at most <see cref="Constants.Ai.Claude.MaxPoints"/>.</summary>
    /// <param name="points">The points as Claude sent them.</param>
    private static IReadOnlyList<ImagePoint> ToPoints(List<AnswerPoint>? points) =>
        [.. (points ?? []).Where(p => p.X is >= 0 and <= 1 && p.Y is >= 0 and <= 1).Take(Constants.Ai.Claude.MaxPoints).Select(p => new ImagePoint(p.X, p.Y))];

    /// <summary>Clips a box to the image.</summary>
    /// <param name="b">The box as Claude sent it.</param>
    /// <returns>The clipped box, or <c>null</c> when nothing of it is inside the image.</returns>
    private static BoundingBox? ToBox(AnswerBox b)
    {
        var (x, width) = Clip(b.X, b.Width);
        var (y, height) = Clip(b.Y, b.Height);
        return width > Constants.Ai.Claude.MinBoxSide && height > Constants.Ai.Claude.MinBoxSide ? new BoundingBox(x, y, width, height) : null;
    }

    /// <summary>Clips one axis of a box to 0..1, leaving values that already fit untouched.</summary>
    /// <param name="start">The box's start on this axis.</param>
    /// <param name="length">The box's length on this axis.</param>
    private static (double Start, double Length) Clip(double start, double length)
    {
        if (start >= 0 && start + length <= 1)
            return (start, length);
        var clipped = Math.Clamp(start, 0, 1);
        return (clipped, Math.Min(start + length, 1) - clipped);
    }

    /// <summary>Claude's answer.</summary>
    /// <param name="IsRoom">Whether the photo shows a room.</param>
    /// <param name="Objects">The objects found.</param>
    private sealed record Answer(bool IsRoom, List<AnswerObject>? Objects);

    /// <summary>One object in Claude's answer.</summary>
    /// <param name="Label">The object's name.</param>
    /// <param name="Category">The kind of object.</param>
    /// <param name="Box">Where it is.</param>
    /// <param name="Color">Its main color.</param>
    /// <param name="Points">Spots on its surface (walls, floors and ceilings).</param>
    private sealed record AnswerObject(string? Label, ObjectCategory Category, AnswerBox? Box, string? Color, List<AnswerPoint>? Points = null);

    /// <summary>A spot in Claude's answer, as fractions of the image.</summary>
    /// <param name="X">Across.</param>
    /// <param name="Y">Down.</param>
    private sealed record AnswerPoint(double X, double Y);

    /// <summary>A box in Claude's answer, as fractions of the image.</summary>
    /// <param name="X">Left edge.</param>
    /// <param name="Y">Top edge.</param>
    /// <param name="Width">Width.</param>
    /// <param name="Height">Height.</param>
    private sealed record AnswerBox(double X, double Y, double Width, double Height);
}
