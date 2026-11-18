using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesignApp.Application.Objects;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;
using F = DesignApp.Infrastructure.Constants.Ai.RoomBuilder.Photo.Fields;
using P = DesignApp.Infrastructure.Constants.Ai.RoomBuilder.Photo;
using RB = DesignApp.Infrastructure.Constants.Ai.RoomBuilder;

namespace DesignApp.Infrastructure.Ai.Claude;

/// <summary>
/// Reading a room photo into a plan, the same on the Claude API and on Claude Code: the message (the plan's rules and
/// the catalog to pick from), the answer's JSON schema (every field required, objects limited to the catalog's keys),
/// and reading the answer.
/// </summary>
internal static class PhotoPlanJson
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static readonly string[] Walls = [P.OnFloor, nameof(PlanWall.Back), nameof(PlanWall.Right), nameof(PlanWall.Front), nameof(PlanWall.Left)];
    private static readonly int[] Turns = [0, 90, 180, 270];
    private static readonly string[] ObjectFields =
        [F.Key, F.Variant, F.X, F.Y, F.Rotation, F.Width, F.Depth, F.Height, F.Color, F.Material, F.Wall, F.Along, F.Elevation];
    private static readonly string[] PlanFields =
        [F.RoomType, F.RoomWidth, F.RoomLength, F.RoomHeight, F.WallColor, F.Walls, F.FloorColor, F.FloorType, F.CeilingLight, F.CameraX, F.CameraY, F.Objects];

    /// <summary>The message: the photo's size, the plan's rules and the catalog.</summary>
    /// <param name="catalog">The objects to pick from.</param>
    /// <param name="width">The photo's width, in pixels.</param>
    /// <param name="height">The photo's height, in pixels.</param>
    public static string Message(IReadOnlyList<CatalogObject> catalog, int width, int height)
    {
        var text = new StringBuilder().AppendLine(P.Size(width, height));

        foreach (var rule in P.Rules)
        {
            text.AppendLine(rule);
        }

        foreach (var o in catalog)
        {
            var variants = (o.Variants ?? []).Select(v => $"{v.Key} = {v.Label} {v.Width} x {v.Depth} x {v.Height}");

            text.AppendLine(P.CatalogLine(o.Key, variants, o.Materials ?? []));
        }

        return text.AppendLine(P.Surfaces).ToString();
    }

    /// <summary>The answer's JSON schema.</summary>
    /// <param name="catalog">The objects it may name.</param>
    public static Dictionary<string, JsonElement> Schema(IReadOnlyList<CatalogObject> catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var number = new { type = "number" };
        var text = new { type = "string" };
        var item = new
        {
            type = "object",
            additionalProperties = false,
            required = ObjectFields,
            properties = new Dictionary<string, object>
            {
                [F.Key] = new { type = "string", @enum = catalog.Select(o => o.Key).ToArray() },
                [F.Variant] = text,
                [F.X] = number,
                [F.Y] = number,
                [F.Rotation] = new { type = "integer", @enum = Turns },
                [F.Width] = number,
                [F.Depth] = number,
                [F.Height] = number,
                [F.Color] = text,
                [F.Material] = text,
                [F.Wall] = new { type = "string", @enum = Walls },
                [F.Along] = number,
                [F.Elevation] = number,
            },
        };

        return new()
        {
            ["type"] = JsonSerializer.SerializeToElement("object"),
            ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
            ["required"] = JsonSerializer.SerializeToElement(PlanFields),
            ["properties"] = JsonSerializer.SerializeToElement(new Dictionary<string, object>
            {
                [F.RoomType] = Choice<RoomType>(),
                [F.RoomWidth] = number,
                [F.RoomLength] = number,
                [F.RoomHeight] = number,
                [F.WallColor] = text,
                [F.Walls] = Choice<WallFinish>(),
                [F.FloorColor] = text,
                [F.FloorType] = Choice<FloorType>(),
                [F.CeilingLight] = Choice<CeilingLight>(),
                [F.CameraX] = number,
                [F.CameraY] = number,
                [F.Objects] = new { type = "array", items = item },
            }),
        };
    }

    /// <summary>Reads the answer.</summary>
    /// <param name="json">The answer's JSON.</param>
    /// <returns>The plan as read, or a failure the user can read when the answer is malformed.</returns>
    public static Result<PhotoPlan> Parse(string json)
    {
        try
        {
            var answer = JsonSerializer.Deserialize<Answer>(json, Json);

            if (answer?.Objects is null || !Enum.IsDefined(answer.RoomType))
            {
                return Result.Failure<PhotoPlan>(RB.Messages.PhotoPlanFailed);
            }

            var objects = answer.Objects
                .Select(o => new PhotoPlanItem(o.Key ?? "", o.Variant ?? "", o.X, o.Y, o.Rotation, o.Width, o.Depth, o.Height, o.Color ?? "", o.Material ?? "",
                    Enum.TryParse<PlanWall>(o.Wall, out var wall) && o.Wall != P.OnFloor ? wall : null, o.Along, o.Elevation))
                .ToList();

            return new PhotoPlan(answer.RoomType, answer.WidthMetres, answer.LengthMetres, answer.HeightMetres, answer.WallColor ?? "", answer.Walls, answer.FloorColor ?? "",
                answer.FloorType, answer.CeilingLight, new OutlinePoint(answer.CameraX, answer.CameraY), objects);
        }
        catch (JsonException)
        {
            return Result.Failure<PhotoPlan>(RB.Messages.PhotoPlanFailed);
        }
    }

    /// <summary>A choice among an enum's names, for the schema.</summary>
    /// <typeparam name="T">The enum.</typeparam>
    private static object Choice<T>()
        where T : struct, Enum
    {
        return new { type = "string", @enum = Enum.GetNames<T>() };
    }

    /// <summary>The answer as Claude gives it.</summary>
    private sealed record Answer(RoomType RoomType, double WidthMetres, double LengthMetres, double HeightMetres, string? WallColor, WallFinish Walls, string? FloorColor,
        FloorType FloorType, CeilingLight CeilingLight, double CameraX, double CameraY, List<AnswerObject>? Objects);

    /// <summary>One object of the answer.</summary>
    private sealed record AnswerObject(string? Key, string? Variant, double X, double Y, int Rotation, double Width, double Depth, double Height, string? Color,
        string? Material, string? Wall, double Along, double Elevation);
}
