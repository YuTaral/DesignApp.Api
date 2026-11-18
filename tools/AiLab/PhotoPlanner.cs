// AiLab photo-plan: the app's "Build in 3D" on photos without uploading them (an upload also runs the paid analysis):
// the same Claude reader (ClaudeRoomBuilderAi) and the same checks and cleanup (PhotoPlanReader), to measure them.
//
//   dotnet run --project tools/AiLab -- photo-plan <folder> <photo>... [--yes | --replay]
//
// Without --yes it only lists the photos. With --yes it is PAID: one Claude vision call per photo (about 2 cents).
// With --replay it is free: each photo's answer.json from an earlier run is read again, through today's checks and cleanup.
// For each photo it writes <folder>/<photo name>/analysis.jpg (the photo as the app prepares it), answer.json (the
// answer as read) and plan.json (the room plan as the app would save it, for a page to load and draw in 3D).
// The API key comes from DesignApp.Api's user secrets and is never printed.
using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic;
using DesignApp.Application.Helpers;
using DesignApp.Application.Objects;
using DesignApp.Infrastructure.Ai.Claude;
using DesignApp.Infrastructure.Builder;
using DesignApp.Infrastructure.Photos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using static DesignApp.Domain.Constants;

namespace AiLab;

/// <summary>The <c>photo-plan</c> command: photos read into room plans by the app's own reader.</summary>
internal static class PhotoPlanner
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    /// <summary>Runs the command.</summary>
    /// <param name="args">The command line, starting with <c>photo-plan</c>.</param>
    /// <returns>The exit code.</returns>
    public static async Task<int> RunAsync(string[] args)
    {
        var paid = args.Contains("--yes");
        var replay = args.Contains("--replay");
        var photos = args.Skip(2).Where(a => a is not ("--yes" or "--replay")).ToList();

        if (args.Length < 3 || photos.Count == 0)
        {
            Console.Error.WriteLine("Usage: photo-plan <folder> <photo>... [--yes | --replay]");

            return 2;
        }

        var folder = Path.GetFullPath(args[1]);

        Console.WriteLine($"{photos.Count} photo(s), one Claude vision call each (about 2 cents).");

        if (!paid && !replay)
        {
            photos.ForEach(p => Console.WriteLine($"  {p}"));
            Console.WriteLine("Nothing was sent. Add --yes to run it (paid).");

            return 0;
        }

        var key = new ConfigurationBuilder().AddUserSecrets(typeof(PhotoPlanner).Assembly, optional: true).Build()["Ai:AnthropicApiKey"];

        if (!replay && string.IsNullOrWhiteSpace(key))
        {
            Console.Error.WriteLine("Ai:AnthropicApiKey is not in the user secrets.");

            return 1;
        }

        using var logs = LoggerFactory.Create(b => b.AddSimpleConsole(o => o.SingleLine = true).SetMinimumLevel(LogLevel.Information));
        var reader = replay ? null : new ClaudeRoomBuilderAi(new AnthropicClient { ApiKey = key! }, logs.CreateLogger<ClaudeRoomBuilderAi>());
        var offered = PhotoPlanReader.Offered(new EmbeddedObjectCatalog());
        var failed = 0;

        foreach (var path in photos)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var into = Path.Combine(folder, $"{Path.GetFileName(Path.GetDirectoryName(path))}-{name}");

            Directory.CreateDirectory(into);
            await using var upload = File.OpenRead(path);
            var stored = new SkiaPhotoProcessor().Process(upload);

            if (!stored.Succeeded)
            {
                Console.Error.WriteLine($"{path}: {stored.Error}");
                failed++;
                continue;
            }

            using var photo = new MemoryStream(stored.Value.Content);
            var image = new SkiaRoomImaging().PrepareForAnalysis(photo);

            await File.WriteAllBytesAsync(Path.Combine(into, "analysis.jpg"), image.Jpeg);
            var read = reader is null
                ? JsonSerializer.Deserialize<PhotoPlan>(await File.ReadAllTextAsync(Path.Combine(into, "answer.json")), Json)
                : await ReadAsync(reader, image, offered, path);

            if (read is null)
            {
                failed++;
                continue;
            }

            var answer = read;
            var ready = PhotoPlanReader.Read(answer, offered);
            var items = ready.Items.Select(i => new
            {
                i.Label, i.Shape, i.X, i.Y, i.Width, i.Depth, i.Height, i.Rotation, Color = i.Color.Value, i.Wall, i.Elevation, i.Form, i.Material,
            });
            var finish = new
            {
                ready.Finish.Walls, ready.Finish.AccentWall, AccentColor = ready.Finish.AccentColor.Value, ready.Finish.AccentFinish,
                CeilingColor = ready.Finish.CeilingColor.Value, TrimColor = ready.Finish.TrimColor.Value, ready.Finish.CeilingLight, ready.Finish.TimeOfDay,
            };
            var plan = new
            {
                answer.RoomType, ready.Width, ready.Length, ready.Height, WallColor = ready.WallColor.Value, FloorColor = ready.FloorColor.Value, answer.FloorType,
                Corner = ViewCorner.Spot, CameraSpot = new { ready.Camera.X, ready.Camera.Y }, Items = items, Finish = finish, ready.LeftOut,
            };

            await File.WriteAllTextAsync(Path.Combine(into, "answer.json"), JsonSerializer.Serialize(answer, Json));
            await File.WriteAllTextAsync(Path.Combine(into, "plan.json"), JsonSerializer.Serialize(plan, Json));
            Console.WriteLine($"{name}: {answer.RoomType} {ready.Width} x {ready.Length} x {ready.Height} m, {answer.Objects.Count} read, {ready.Items.Count} kept, " +
                $"left out: {(ready.LeftOut.Count == 0 ? "none" : string.Join(", ", ready.LeftOut))}");
        }

        return failed == 0 ? 0 : 1;
    }

    /// <summary>A photo read into a plan by Claude (paid), or <c>null</c> with the error written when it fails.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="image">The photo, prepared.</param>
    /// <param name="offered">The catalog objects it may use.</param>
    /// <param name="path">The photo's path, for the error.</param>
    private static async Task<PhotoPlan?> ReadAsync(ClaudeRoomBuilderAi reader, AnalysisImage image, IReadOnlyList<CatalogObject> offered, string path)
    {
        var read = await reader.PlanFromPhotoAsync(image, offered, CancellationToken.None);

        if (!read.Succeeded)
        {
            Console.Error.WriteLine($"{path}: {read.Error}");

            return null;
        }

        return read.Value;
    }
}
