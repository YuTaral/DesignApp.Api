// AiLab depth: measures the depth map of every layout of a built-in library design with MoGe-2 on fal.
//
//   dotnet run --project tools/AiLab -- depth <design folder> [--yes] [--force]
//
// <design folder> is a design under src/DesignApp.Infrastructure/Library (such as .../Library/minimalist). For each
// layout it writes <layout>.depth.png and adds the camera to <layout>.objects.json, so sample rooms open in 3D with no
// cost. Layouts that already have a depth map are skipped unless --force. Without --yes it only prints what it would
// measure and the cost (each call is about $0.01: the photo is sent at 1 megapixel at most).
using System.Text.Json;
using System.Text.Json.Nodes;
using DesignApp.Application.Objects;
using DesignApp.Infrastructure.Ai.Fal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AiLab;

/// <summary>The <c>depth</c> command: depth maps for a library design's layouts.</summary>
internal static class Depth
{
    private const decimal PricePerCall = 0.01m;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>Runs the command.</summary>
    /// <param name="args">The command line, starting with <c>depth</c>.</param>
    /// <returns>The exit code.</returns>
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length < 2 || !Directory.Exists(args[1]))
        {
            Console.Error.WriteLine("Usage: depth <design folder> [--yes] [--force]");
            return 2;
        }
        var folder = Path.GetFullPath(args[1]);
        var paid = args.Contains("--yes");
        var force = args.Contains("--force");
        var layouts = Directory.GetFiles(folder, "*.jpg", SearchOption.AllDirectories)
            .Where(photo => force || !File.Exists(Path.ChangeExtension(photo, null) + ".depth.png"))
            .Order(StringComparer.Ordinal)
            .ToList();
        Console.WriteLine($"{layouts.Count} layout(s) to measure with MoGe-2: about ${layouts.Count * PricePerCall:0.00}.");
        if (!paid || layouts.Count == 0)
        {
            if (layouts.Count > 0)
                Console.WriteLine("Nothing was measured. Add --yes to run it (paid).");
            return 0;
        }

        var key = new ConfigurationBuilder().AddUserSecrets(typeof(Depth).Assembly, optional: true).Build()["Ai:FalKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            Console.Error.WriteLine("Ai:FalKey is not in the user secrets.");
            return 1;
        }
        using var http = new HttpClient { Timeout = DesignApp.Infrastructure.Constants.Ai.Depth.CallTimeout };
        http.DefaultRequestHeaders.Authorization = new(DesignApp.Infrastructure.Constants.Ai.FalAuthScheme, key);
        using var logs = LoggerFactory.Create(b => b.AddSimpleConsole(o => o.SingleLine = true).SetMinimumLevel(LogLevel.Warning));
        var provider = new FalDepthProvider(http, logs.CreateLogger<FalDepthProvider>());

        var failed = 0;
        foreach (var photoPath in layouts)
        {
            var stem = Path.ChangeExtension(photoPath, null);
            var objectsPath = stem + ".objects.json";
            var file = JsonNode.Parse(await File.ReadAllTextAsync(objectsPath))!.AsObject();
            var (width, height) = ((int)file["width"]!, (int)file["height"]!);
            var input = new DepthInput(await File.ReadAllBytesAsync(photoPath), width, height, [], []);
            var measured = await provider.MeasureAsync(input, CancellationToken.None);
            if (!measured.Succeeded)
            {
                Console.Error.WriteLine($"{photoPath}: {measured.Error}");
                failed++;
                continue;
            }
            await File.WriteAllBytesAsync(stem + ".depth.png", measured.Value.Png);
            file["depth"] = new JsonObject { ["fovX"] = Math.Round(measured.Value.FovX, 3), ["fovY"] = Math.Round(measured.Value.FovY, 3) };
            await File.WriteAllTextAsync(objectsPath, file.ToJsonString(Json) + "\n");
            Console.WriteLine($"{Path.GetRelativePath(folder, photoPath)}: fov {measured.Value.FovX:0.0} x {measured.Value.FovY:0.0}");
        }
        return failed == 0 ? 0 : 1;
    }
}
