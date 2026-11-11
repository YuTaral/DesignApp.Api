// AiLab: runs the app's real AI pipeline on one photo outside the API.
//
//   dotnet run --project tools/AiLab -- record <photo.jpg> <folder>   PAID: one Claude call + the SAM 3 requests
//   dotnet run --project tools/AiLab -- replay <folder>               free: answers SAM 3 from the recording
//
// record saves the analysis image, Claude's objects (inventory.json) and every SAM 3 request and response
// (sam/<hash>.json). replay feeds those back to the same FalSegmentationProvider, so changes to the mask
// picking or the label-map cleanup can be measured without paying; a request it has no answer for is
// listed (and answered with no masks) so you can see when a change would need new paid requests.
// Both write labels.png (the label map) and objects.json (the objects as the app would store them).
// The API keys come from DesignApp.Api's user secrets and are never printed.
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Anthropic;
using DesignApp.Application.Objects;
using DesignApp.Domain.Extensions;
using DesignApp.Domain.Objects;
using DesignApp.Infrastructure.Ai;
using DesignApp.Infrastructure.Ai.Claude;
using DesignApp.Infrastructure.Ai.Fal;
using DesignApp.Infrastructure.Photos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static DesignApp.Domain.Constants;

if (args.Length < 2 || args[0] is not ("record" or "replay"))
{
    Console.Error.WriteLine("Usage: record <photo.jpg> <folder> | replay <folder>");
    return 2;
}

var recording = args[0] == "record";
var folder = Path.GetFullPath(recording ? args[2] : args[1]);
var samFolder = Path.Combine(folder, "sam");
Directory.CreateDirectory(samFolder);
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
using var logs = LoggerFactory.Create(b => b.AddSimpleConsole(o => o.SingleLine = true).SetMinimumLevel(LogLevel.Information));
var settings = new FixedSettings(new AiSettings());
var imaging = new SkiaRoomImaging();

// 1. The analysis image, prepared exactly as the app prepares it (recorded once, so replays use the same bytes).
var imagePath = Path.Combine(folder, "analysis.jpg");
AnalysisImage image;
if (recording)
{
    await using var photo = File.OpenRead(args[1]);
    image = imaging.PrepareForAnalysis(photo);
    await File.WriteAllBytesAsync(imagePath, image.Jpeg);
    await File.WriteAllTextAsync(Path.Combine(folder, "analysis.json"), JsonSerializer.Serialize(new { image.Width, image.Height }, json));
}
else
{
    var size = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "analysis.json"))).RootElement;
    image = new AnalysisImage(await File.ReadAllBytesAsync(imagePath), size.GetProperty("width").GetInt32(), size.GetProperty("height").GetInt32());
}

// 2. Claude's objects: asked for when recording, read back when replaying.
var secrets = new ConfigurationBuilder().AddUserSecrets(typeof(FixedSettings).Assembly, optional: true).Build();
var inventoryPath = Path.Combine(folder, "inventory.json");
IReadOnlyList<InventoryItem> items;
if (recording)
{
    var key = secrets["Ai:AnthropicApiKey"];
    if (string.IsNullOrWhiteSpace(key)) { Console.Error.WriteLine("Ai:AnthropicApiKey is not in the user secrets."); return 1; }
    IAnthropicClient client = new AnthropicClient { ApiKey = key };
    if (secrets["Ai:AnthropicWorkspaceId"] is { Length: > 0 } workspace)
        client = client.WithOptions(o => o with { ExtraHeaders = new Dictionary<string, string> { [DesignApp.Infrastructure.Constants.Ai.WorkspaceHeader] = workspace.Trim() } });
    var claude = new ClaudeInventoryProvider(client, Options.Create(new AiOptions()), settings, logs.CreateLogger<ClaudeInventoryProvider>());
    var listed = await claude.ListObjectsAsync(image, CancellationToken.None);
    if (!listed.Succeeded) { Console.Error.WriteLine($"Claude: {listed.Error}"); return 1; }
    items = listed.Value;
    await File.WriteAllTextAsync(inventoryPath, JsonSerializer.Serialize(items.Select(Saved.From), json));
}
else
{
    items = [.. JsonSerializer.Deserialize<List<Saved>>(await File.ReadAllTextAsync(inventoryPath), json)!.Select(s => s.ToItem())];
}
items = [.. items.Where(i => AnalysisScope.WholeRoom.Includes(i.Category)).Take(DesignApp.Application.Constants.Analysis.MaxObjects)];
Console.WriteLine($"{items.Count} objects: {string.Join(", ", items.Select(i => i.Label))}");

// 3. SAM 3 through the app's own provider, with every request recorded or answered from the recording.
var missing = new List<string>();
HttpMessageHandler handler = recording
    ? new RecordingHandler(samFolder, new HttpClientHandler())
    : new ReplayHandler(samFolder, missing);
using var http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(3) };
if (recording)
{
    var fal = secrets["Ai:FalKey"];
    if (string.IsNullOrWhiteSpace(fal)) { Console.Error.WriteLine("Ai:FalKey is not in the user secrets."); return 1; }
    http.DefaultRequestHeaders.Authorization = new(DesignApp.Infrastructure.Constants.Ai.FalAuthScheme, fal);
}
var sam = new FalSegmentationProvider(http, settings, logs.CreateLogger<FalSegmentationProvider>());
var masks = await sam.SegmentAsync(image, items, CancellationToken.None);

// 4. The label map, built exactly as the app builds it.
var outlined = items.Zip(masks).Where(p => p.Second is not null).Select(p => new SegmentedObject(p.First, p.Second!)).ToList();
var map = imaging.BuildLabelMap(image, outlined);
await File.WriteAllBytesAsync(Path.Combine(folder, "labels.png"), map.Png);
await File.WriteAllTextAsync(Path.Combine(folder, "objects.json"), JsonSerializer.Serialize(map.Objects.Select(o => new
{
    o.Index, o.Label, Category = o.Category.ToString(), o.Box.X, o.Box.Y, o.Box.Width, o.Box.Height,
}), json));
Console.WriteLine($"label map: {map.Objects.Count} objects, {outlined.Count} of {items.Count} outlined");
Console.WriteLine(missing.Count == 0 ? "every SAM 3 request was answered" : $"{missing.Count} SAM 3 request(s) not in the recording: {string.Join("; ", missing)}");
return 0;

/// <summary>AI settings that don't come from a database.</summary>
internal sealed class FixedSettings(AiSettings settings) : IAiSettingsStore
{
    public Task<AiSettings> GetAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(settings);
    }
}

/// <summary>An inventory item as saved to inventory.json.</summary>
internal sealed record Saved(string Label, string Category, double X, double Y, double Width, double Height, string Color, List<double[]>? Points)
{
    public static Saved From(InventoryItem i)
    {
        return new(i.Label, i.Category.ToString(), i.Box.X, i.Box.Y, i.Box.Width, i.Box.Height, i.Color.Value,
            i.Points?.Select(p => new[] { p.X, p.Y }).ToList());
    }

    public InventoryItem ToItem()
    {
        return new(Label, Enum.Parse<ObjectCategory>(Category), new BoundingBox(X, Y, Width, Height), HexColor.Parse(Color),
            Points?.Select(p => new ImagePoint(p[0], p[1])).ToList());
    }
}

/// <summary>Sends SAM 3 requests and saves each request with its response.</summary>
internal sealed class RecordingHandler(string folder, HttpMessageHandler inner) : DelegatingHandler(inner)
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        var response = await base.SendAsync(request, cancellationToken);
        var answer = await response.Content.ReadAsStringAsync(cancellationToken);
        if (request.Method == HttpMethod.Post)
            await File.WriteAllTextAsync(Path.Combine(folder, Hash.Of(body) + ".json"), JsonSerializer.Serialize(new
            {
                status = (int)response.StatusCode,
                request = Hash.WithoutImage(body),
                response = answer,
            }), cancellationToken);
        var copy = new HttpResponseMessage(response.StatusCode) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        return copy;
    }
}

/// <summary>Answers SAM 3 requests from the recording; unknown ones get no masks and are listed.</summary>
internal sealed class ReplayHandler(string folder, List<string> missing) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        var file = Path.Combine(folder, Hash.Of(body) + ".json");
        if (!File.Exists(file))
        {
            lock (missing) missing.Add(Hash.Describe(body));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"masks":[]}""", Encoding.UTF8, "application/json") };
        }
        var saved = JsonDocument.Parse(await File.ReadAllTextAsync(file, cancellationToken)).RootElement;
        return new HttpResponseMessage((HttpStatusCode)saved.GetProperty("status").GetInt32())
        {
            Content = new StringContent(saved.GetProperty("response").GetString()!, Encoding.UTF8, "application/json"),
        };
    }
}

/// <summary>Keys and short descriptions of SAM 3 request bodies.</summary>
internal static class Hash
{
    public static string Of(string body)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)))[..16].ToLowerInvariant();
    }

    /// <summary>The body without the base64 photo, to keep the recording readable.</summary>
    public static string WithoutImage(string body)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(body)!;
        node["image_url"] = "(the analysis image)";
        return node.ToJsonString();
    }

    public static string Describe(string body)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(body)!;
        var prompt = node["prompt"]?.GetValue<string>();
        var boxes = node["box_prompts"]?.AsArray().Count ?? 0;
        return string.IsNullOrEmpty(prompt) ? $"box-only ({boxes} box{(boxes == 1 ? "" : "es")})" : $"\"{prompt}\"";
    }
}
