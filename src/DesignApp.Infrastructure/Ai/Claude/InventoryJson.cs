using System.Text.Json;
using System.Text.Json.Serialization;
using DesignApp.Application.Analysis;
using DesignApp.Application.Common;
using DesignApp.Domain.Common;
using DesignApp.Domain.Rooms;

namespace DesignApp.Infrastructure.Ai.Claude;

/// <summary>
/// The JSON contract between Claude and the app: the schema Claude must answer with, and the code
/// that turns its answer into inventory items. Kept apart from the API call so it can be tested.
/// </summary>
internal static class InventoryJson
{
    /// <summary>Shown when Claude says the photo isn't of a room.</summary>
    public const string NotARoom = "This doesn't look like a photo of a room. Upload a photo of a room's interior.";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The JSON schema for Claude's structured output.</summary>
    public static Dictionary<string, JsonElement> Schema { get; } = new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
        ["required"] = JsonSerializer.SerializeToElement(new[] { "isRoom", "objects" }),
        ["properties"] = JsonSerializer.SerializeToElement(new Dictionary<string, object>
        {
            ["isRoom"] = new { type = "boolean", description = "True when the photo shows the interior of a room." },
            ["objects"] = new
            {
                type = "array",
                items = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new[] { "label", "category", "box", "color", "points" },
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["label"] = new { type = "string", description = "Short name a person would use, such as \"grey sofa\" or \"left wall\"." },
                        ["category"] = new { type = "string", @enum = Enum.GetNames<ObjectCategory>() },
                        ["box"] = new Dictionary<string, object>
                        {
                            ["type"] = "object",
                            ["additionalProperties"] = false,
                            ["required"] = new[] { "x", "y", "width", "height" },
                            ["description"] = "Bounding box as fractions (0 to 1) of the image width and height, from the top-left corner.",
                            ["properties"] = new
                            {
                                x = new { type = "number" },
                                y = new { type = "number" },
                                width = new { type = "number" },
                                height = new { type = "number" },
                            },
                        },
                        ["color"] = new { type = "string", description = "The object's main color as #RRGGBB." },
                        ["points"] = new Dictionary<string, object>
                        {
                            ["type"] = "array",
                            ["description"] = "For walls, floors and ceilings: 2 or 3 spots on the visible surface itself, as fractions (0 to 1) of the image. Empty for every other object.",
                            ["items"] = new Dictionary<string, object>
                            {
                                ["type"] = "object",
                                ["additionalProperties"] = false,
                                ["required"] = new[] { "x", "y" },
                                ["properties"] = new { x = new { type = "number" }, y = new { type = "number" } },
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
            return Result.Failure<IReadOnlyList<InventoryItem>>(NotARoom);

        var items = new List<InventoryItem>();
        foreach (var o in answer.Objects ?? [])
        {
            if (string.IsNullOrWhiteSpace(o.Label) || o.Box is null || ToBox(o.Box) is not { } box)
                continue;
            var color = HexColor.TryParse(o.Color, out var parsed) ? parsed : HexColor.Parse("#808080");
            items.Add(new InventoryItem(o.Label.Trim(), o.Category, box, color, ToPoints(o.Points)));
        }
        return items;
    }

    /// <summary>The points inside the image, at most <see cref="MaxPoints"/>.</summary>
    /// <param name="points">The points as Claude sent them.</param>
    private static IReadOnlyList<ImagePoint> ToPoints(List<AnswerPoint>? points) =>
        [.. (points ?? []).Where(p => p.X is >= 0 and <= 1 && p.Y is >= 0 and <= 1).Take(MaxPoints).Select(p => new ImagePoint(p.X, p.Y))];

    /// <summary>How many points per object are kept.</summary>
    internal const int MaxPoints = 3;

    /// <summary>Clips a box to the image.</summary>
    /// <param name="b">The box as Claude sent it.</param>
    /// <returns>The clipped box, or <c>null</c> when nothing of it is inside the image.</returns>
    private static BoundingBox? ToBox(AnswerBox b)
    {
        var (x, width) = Clip(b.X, b.Width);
        var (y, height) = Clip(b.Y, b.Height);
        return width > 0.001 && height > 0.001 ? new BoundingBox(x, y, width, height) : null;
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
