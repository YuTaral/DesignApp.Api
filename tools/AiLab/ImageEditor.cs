// AiLab edit: one image turned by the room builder's image model (FLUX 3 Image Klein's editing on fal) with a prompt.
//
//   dotnet run --project tools/AiLab -- edit <image> <prompt.txt> <out.jpg> [--width 2048] [--height 1024] [--seed n]
//                                       [--boxes <boxes.json>] [--yes]
//   dotnet run --project tools/AiLab -- fill <image> <mask.png> <prompt.txt> <out.jpg> [--seed n] [--yes]
//
// Without --yes they only print what they would send. With --yes they are PAID: edit $0.009 a 1K photo up to $0.036 at
// 4K; fill (FLUX.1 Pro Fill, which redraws only the mask's white part) $0.05 a megapixel. Used to try a 360-degree
// panorama of a built room: its equirectangular 3D view made into one photo (with each object's box), and the seam where
// its ends meet redrawn.
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace AiLab;

/// <summary>The <c>edit</c> command: an image made into a photo by FLUX 3 Image Klein, at a chosen size.</summary>
internal static class ImageEditor
{
    /// <summary>The model, on fal's synchronous endpoint.</summary>
    private const string Endpoint = "https://fal.run/blackforestlabs/flux-3/klein/edit-image";

    /// <summary>The inpainting model, on fal's synchronous endpoint.</summary>
    private const string FillEndpoint = "https://fal.run/fal-ai/flux-pro/v1/fill";

    /// <summary>Runs the command.</summary>
    /// <param name="args">The command line, starting with <c>edit</c>.</param>
    /// <returns>The exit code.</returns>
    public static async Task<int> RunAsync(string[] args)
    {
        if (args[0] == "fill")
        {
            return await FillAsync(args);
        }

        if (args.Length < 4)
        {
            Console.Error.WriteLine("Usage: edit <image> <prompt.txt> <out.jpg> [--width n] [--height n] [--seed n] [--yes]");

            return 2;
        }

        var image = Path.GetFullPath(args[1]);
        var prompt = await File.ReadAllTextAsync(args[2]);
        var output = Path.GetFullPath(args[3]);
        var (width, height) = (2048, 1024);
        int? seed = null;
        string? boxes = null;
        var paid = false;

        for (var i = 4; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--width": width = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--height": height = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--seed": seed = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--boxes": boxes = await File.ReadAllTextAsync(args[++i]); break;
                case "--yes": paid = true; break;
                default: Console.Error.WriteLine($"Unknown option {args[i]}"); return 2;
            }
        }

        Console.WriteLine($"{Path.GetFileName(image)} -> {width}x{height}, {prompt.Length} characters of prompt{(boxes is null ? "" : ", with object boxes")}.");

        if (!paid)
        {
            Console.WriteLine("Not sent: add --yes to make it (paid).");

            return 0;
        }

        var body = new Dictionary<string, object>
        {
            ["prompt"] = prompt,
            ["image_urls"] = new[] { await DataUriAsync(image) },
            ["image_size"] = new { width, height },
            ["output_format"] = "jpeg",
            ["enable_prompt_expansion"] = false,
        };

        if (seed is not null)
        {
            body["seed"] = seed;
        }

        if (boxes is not null)
        {
            body["bounding_boxes"] = JsonSerializer.Deserialize<JsonElement>(boxes);
        }

        return await SendAsync(Endpoint, body, output);
    }

    /// <summary>The <c>fill</c> command: redraws the white part of a mask over an image, matching what is around it.</summary>
    /// <param name="args">The command line, starting with <c>fill</c>.</param>
    /// <returns>The exit code.</returns>
    private static async Task<int> FillAsync(string[] args)
    {
        if (args.Length < 5)
        {
            Console.Error.WriteLine("Usage: fill <image> <mask.png> <prompt.txt> <out.jpg> [--seed n] [--yes]");

            return 2;
        }

        var body = new Dictionary<string, object>
        {
            ["prompt"] = await File.ReadAllTextAsync(args[3]),
            ["image_url"] = await DataUriAsync(Path.GetFullPath(args[1])),
            ["mask_url"] = await DataUriAsync(Path.GetFullPath(args[2])),
            ["output_format"] = "jpeg",
        };
        var paid = false;

        for (var i = 5; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--seed": body["seed"] = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--yes": paid = true; break;
                default: Console.Error.WriteLine($"Unknown option {args[i]}"); return 2;
            }
        }

        Console.WriteLine($"fill {Path.GetFileName(args[1])} under {Path.GetFileName(args[2])}.");

        if (!paid)
        {
            Console.WriteLine("Not sent: add --yes to make it (paid).");

            return 0;
        }

        return await SendAsync(FillEndpoint, body, Path.GetFullPath(args[4]));
    }

    /// <summary>Sends a paid request with the fal key from the user secrets and saves the first image it gives.</summary>
    /// <param name="endpoint">The model.</param>
    /// <param name="body">The request.</param>
    /// <param name="output">Where the image goes.</param>
    /// <returns>The exit code.</returns>
    private static async Task<int> SendAsync(string endpoint, Dictionary<string, object> body, string output)
    {
        var key = new ConfigurationBuilder().AddUserSecrets(typeof(ImageEditor).Assembly, optional: true).Build()["Ai:FalKey"];

        if (string.IsNullOrWhiteSpace(key))
        {
            Console.Error.WriteLine("Ai:FalKey is not in the user secrets.");

            return 1;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

        http.DefaultRequestHeaders.Authorization = new(DesignApp.Infrastructure.Constants.Ai.FalAuthScheme, key);
        using var response = await http.PostAsJsonAsync(endpoint, body);

        if (!response.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"fal answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

            return 1;
        }

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var url = result.GetProperty("images")[0].GetProperty("url").GetString()!;

        await File.WriteAllBytesAsync(output, await http.GetByteArrayAsync(url));
        Console.WriteLine($"saved {output}");

        return 0;
    }

    /// <summary>A local image as a data URI of its own type, so the model reads it without uploading it anywhere.</summary>
    /// <param name="path">The image.</param>
    private static async Task<string> DataUriAsync(string path)
    {
        var type = Path.GetExtension(path).ToLowerInvariant() is ".png" ? "image/png" : "image/jpeg";

        return $"data:{type};base64,{Convert.ToBase64String(await File.ReadAllBytesAsync(path))}";
    }
}
