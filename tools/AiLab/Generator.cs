// AiLab generate: makes the room photos of a built-in library design with an image model on fal.
//
//   dotnet run --project tools/AiLab -- generate <prompts.json> <folder> [--rooms living-room,kitchen]
//                                       [--candidates 3] [--reference <photo>] [--yes]
//
// Without --yes it only prints what it would make and what that costs. With --yes it is PAID (the price per image is
// in prompts.json). A room without a reference is made from text (the first room of a house); with --reference
// (the chosen photo of that first room) every room is made by the edit model from it, so all rooms share the same
// floor, walls, light and style. Candidates are saved as <folder>/<room>-<n>.jpg to pick from.
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace AiLab;

/// <summary>The <c>generate</c> command: room photos for a library design, from its prompts file.</summary>
internal static class Generator
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>Runs the command.</summary>
    /// <param name="args">The command line, starting with <c>generate</c>.</param>
    /// <returns>The exit code.</returns>
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: generate <prompts.json> <folder> [--rooms a,b] [--candidates n] [--reference <photo>] [--yes]");
            return 2;
        }
        var prompts = JsonSerializer.Deserialize<HousePrompts>(await File.ReadAllTextAsync(args[1]), Json)
            ?? throw new InvalidDataException("The prompts file is empty.");
        var folder = Path.GetFullPath(args[2]);
        var rooms = prompts.Rooms;
        var candidates = 3;
        string? reference = null;
        var paid = false;
        for (var i = 3; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--rooms": var wanted = args[++i].Split(','); rooms = [.. rooms.Where(r => wanted.Contains(r.Id))]; break;
                case "--candidates": candidates = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--reference": reference = Path.GetFullPath(args[++i]); break;
                case "--yes": paid = true; break;
                default: Console.Error.WriteLine($"Unknown option {args[i]}"); return 2;
            }
        }
        if (rooms.Count == 0 || candidates is < 1 or > 4)
        {
            Console.Error.WriteLine("No rooms chosen, or --candidates is not 1 to 4.");
            return 2;
        }

        var images = rooms.Count * candidates;
        Console.WriteLine($"{rooms.Count} room(s) x {candidates} candidate(s) = {images} image(s) with " +
            $"{(reference is null ? prompts.Model : prompts.EditModel)} at {prompts.Resolution}: about ${images * prompts.PricePerImage:0.00}.");
        foreach (var room in rooms)
            Console.WriteLine($"  {room.Id}: {Prompt(prompts, room, reference is not null)}");
        if (!paid)
        {
            Console.WriteLine("Nothing was generated. Add --yes to run it (paid).");
            return 0;
        }

        var key = new ConfigurationBuilder().AddUserSecrets(typeof(Generator).Assembly, optional: true).Build()["Ai:FalKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            Console.Error.WriteLine("Ai:FalKey is not in the user secrets.");
            return 1;
        }
        Directory.CreateDirectory(folder);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.Authorization = new(DesignApp.Infrastructure.Constants.Ai.FalAuthScheme, key);
        var referenceUri = reference is null ? null : DataUri(reference);

        foreach (var room in rooms)
        {
            var body = new Dictionary<string, object>
            {
                ["prompt"] = Prompt(prompts, room, referenceUri is not null),
                ["num_images"] = candidates,
                ["aspect_ratio"] = prompts.AspectRatio,
                ["resolution"] = prompts.Resolution,
                ["output_format"] = "jpeg",
            };
            if (referenceUri is not null)
                body["image_urls"] = new[] { referenceUri };
            var endpoint = $"https://fal.run/{(referenceUri is null ? prompts.Model : prompts.EditModel)}";

            using var response = await http.PostAsJsonAsync(endpoint, body);
            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"{room.Id}: fal answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
                return 1;
            }
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var n = 0;
            foreach (var image in result.GetProperty("images").EnumerateArray())
            {
                var file = Path.Combine(folder, $"{room.Id}-{++n}.jpg");
                await File.WriteAllBytesAsync(file, await http.GetByteArrayAsync(image.GetProperty("url").GetString()));
                Console.WriteLine($"{room.Id}: saved {file}");
            }
            await File.WriteAllTextAsync(Path.Combine(folder, $"{room.Id}.prompt.txt"), (string)body["prompt"]);
        }
        return 0;
    }

    /// <summary>The full prompt for one room: the house's shared style, then the room's furniture.</summary>
    /// <param name="prompts">The house.</param>
    /// <param name="room">The room.</param>
    /// <param name="fromReference">Whether the reference photo of the house is sent with it.</param>
    private static string Prompt(HousePrompts prompts, RoomPrompt room, bool fromReference)
    {
        var start = fromReference
            ? $"{prompts.SameHouse} Show the {room.Title.ToLowerInvariant()} of this house."
            : $"Show the {room.Title.ToLowerInvariant()} of a home.";
        return $"{start} {room.Prompt} {prompts.Style}";
    }

    /// <summary>A local photo as a data URI, so the edit model can read it without uploading it anywhere.</summary>
    /// <param name="path">The photo.</param>
    private static string DataUri(string path)
    {
        var type = Path.GetExtension(path).ToLowerInvariant() is ".png" ? "image/png" : "image/jpeg";
        return $"data:{type};base64,{Convert.ToBase64String(File.ReadAllBytes(path))}";
    }

    /// <summary>A house's prompts file.</summary>
    /// <param name="Model">The fal text-to-image endpoint, such as <c>fal-ai/nano-banana-2</c>.</param>
    /// <param name="EditModel">The fal endpoint that takes the reference photo, such as <c>fal-ai/nano-banana-2/edit</c>.</param>
    /// <param name="Resolution">The size asked for, such as <c>2K</c>.</param>
    /// <param name="AspectRatio">Such as <c>4:3</c>.</param>
    /// <param name="PricePerImage">What one image costs at that size, in dollars, for the estimate.</param>
    /// <param name="Style">The shared look, added to every prompt.</param>
    /// <param name="SameHouse">The instruction sent with the reference photo.</param>
    /// <param name="Rooms">The rooms.</param>
    private sealed record HousePrompts(string Model, string EditModel, string Resolution, string AspectRatio, decimal PricePerImage,
        string Style, string SameHouse, IReadOnlyList<RoomPrompt> Rooms);

    /// <summary>One room of the prompts file.</summary>
    /// <param name="Id">The library room id, such as <c>kitchen</c>.</param>
    /// <param name="Title">Such as "Kitchen".</param>
    /// <param name="Prompt">What is in the room.</param>
    private sealed record RoomPrompt(string Id, string Title, string Prompt);
}
