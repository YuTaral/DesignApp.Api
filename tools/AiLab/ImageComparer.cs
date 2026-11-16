// AiLab compare-images: sends one room's 3D view and one prompt to several image models on fal, to compare the photos
// and what they cost before choosing the room builder's model.
//
//   dotnet run --project tools/AiLab -- compare-images <guide.png> <prompt.txt> <folder> [--models a,b] [--yes]
//
// Without --yes it only prints what it would send and what that costs. With --yes it is PAID. Each photo is saved as
// <folder>/<model>.jpg with how long it took.
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace AiLab;

/// <summary>The <c>compare-images</c> command: one 3D view through several image models.</summary>
internal static class ImageComparer
{
    /// <summary>The models it knows: their fal endpoint, price as fal lists it, and their extra settings.</summary>
    private static readonly Dictionary<string, (string Endpoint, string Price, Dictionary<string, object> Settings)> Models = new()
    {
        ["kontext-pro"] = ("fal-ai/flux-pro/kontext", "$0.04 per image", new() { ["aspect_ratio"] = "4:3", ["safety_tolerance"] = "2" }),
        ["kontext-dev"] = ("fal-ai/flux-kontext/dev", "$0.025 per megapixel", new() { ["resolution_mode"] = "match_input" }),
        ["flux-dev-i2i"] = ("fal-ai/flux/dev/image-to-image", "$0.03 per megapixel", new() { ["strength"] = 0.85 }),
    };

    /// <summary>Runs the command.</summary>
    /// <param name="args">The command line, starting with <c>compare-images</c>.</param>
    /// <returns>The exit code.</returns>
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine($"Usage: compare-images <guide.png> <prompt.txt> <folder> [--models {string.Join(',', Models.Keys)}] [--yes]");
            return 2;
        }
        var (guide, prompt, folder) = (Path.GetFullPath(args[1]), File.ReadAllText(args[2]).Trim(), Path.GetFullPath(args[3]));
        var models = Models.Keys.ToList();
        var paid = false;
        for (var i = 4; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--models": models = [.. args[++i].Split(',')]; break;
                case "--yes": paid = true; break;
            }
        }
        if (models.FirstOrDefault(m => !Models.ContainsKey(m)) is { } unknown)
        {
            Console.Error.WriteLine($"Unknown model {unknown}. Known: {string.Join(", ", Models.Keys)}");
            return 2;
        }

        foreach (var model in models)
            Console.WriteLine($"{model}: {Models[model].Endpoint} ({Models[model].Price})");
        if (!paid)
        {
            Console.WriteLine("Nothing was generated. Add --yes to run it (paid).");
            return 0;
        }

        var key = new ConfigurationBuilder().AddUserSecrets(typeof(ImageComparer).Assembly, optional: true).Build()["Ai:FalKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            Console.Error.WriteLine("Ai:FalKey is not in the user secrets.");
            return 1;
        }
        Directory.CreateDirectory(folder);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.Authorization = new(DesignApp.Infrastructure.Constants.Ai.FalAuthScheme, key);
        var image = $"data:image/png;base64,{Convert.ToBase64String(File.ReadAllBytes(guide))}";

        foreach (var model in models)
        {
            var (endpoint, price, settings) = Models[model];
            var body = new Dictionary<string, object>(settings) { ["prompt"] = prompt, ["image_url"] = image, ["output_format"] = "jpeg" };
            var started = Stopwatch.GetTimestamp();
            using var response = await http.PostAsJsonAsync($"https://fal.run/{endpoint}", body);
            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"{model}: fal answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
                continue;
            }
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var first = result.GetProperty("images")[0];
            var file = Path.Combine(folder, $"{model}.jpg");
            await File.WriteAllBytesAsync(file, await http.GetByteArrayAsync(first.GetProperty("url").GetString()));
            var size = first.TryGetProperty("width", out var w) && first.TryGetProperty("height", out var h) ? $"{w}×{h}" : "size not given";
            var seconds = Stopwatch.GetElapsedTime(started).TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture);
            Console.WriteLine($"{model}: {size} in {seconds} s ({price}) → {file}");
        }
        return 0;
    }
}
