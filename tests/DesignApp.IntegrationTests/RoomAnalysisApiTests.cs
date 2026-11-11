using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DesignApp.Tests;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using SkiaSharp;

namespace DesignApp.IntegrationTests;

/// <summary>
/// The analysis pipeline through the real API, database, storage and background worker, with the
/// fake AI providers: upload, wait for Ready, read the label map, and watch progress over SignalR.
/// </summary>
/// <param name="factory">The API running against test containers.</param>
public class RoomAnalysisApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>This test's browser cookies, shared by its HTTP client and its SignalR connections.</summary>
    private readonly CookieContainer _cookies = new();

    /// <summary>The browser's HTTP client, keeping its cookies where the hub connections use them too.</summary>
    private HttpClient Client => field ??=
        factory.CreateDefaultClient(factory.ClientOptions.BaseAddress, new CookieContainerHandler(_cookies));

    [Fact]
    public async Task An_uploaded_room_is_analyzed_in_the_background()
    {
        var created = await UploadAsync();

        var room = await WaitForStatusAsync(created.Id, "Ready");

        Assert.NotEmpty(room.Objects);
        Assert.Equal(DesignApp.Api.Constants.Routes.RoomLabelMapUrl(room.Id, 0), room.LabelMapUrl);
        Assert.Equal((0, "WholeRoom", "Fake"), (room.LabelMapRevision, room.Scope, room.AiMode));
        var sofa = room.Objects.Single(o => o.GetProperty("label").GetString() == "sofa");
        Assert.Equal("Furniture", sofa.GetProperty("category").GetString());
        Assert.Matches("^#[0-9A-F]{6}$", sofa.GetProperty("dominantColor").GetString());
    }

    [Fact]
    public async Task The_label_map_holds_an_object_index_per_pixel()
    {
        var room = await WaitForStatusAsync((await UploadAsync()).Id, "Ready");

        using var response = await Client.GetAsync(room.LabelMapUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(DesignApp.Application.Constants.ContentTypes.Png, response.Content.Headers.ContentType?.MediaType);
        using var codec = SKCodec.Create(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        Assert.Equal((1200, 800), (codec.Info.Width, codec.Info.Height));
        using var labels = SKBitmap.Decode(codec, new SKImageInfo(1200, 800, SKColorType.Gray8, SKAlphaType.Opaque));
        var indexes = labels.GetPixelSpan().ToArray().Where(v => v != 0).Distinct().Order();
        Assert.Equal(room.Objects.Select(o => (byte)o.GetProperty("index").GetInt32()).Order(), indexes);
    }

    [Fact]
    public async Task There_is_no_label_map_for_an_unknown_room()
    {
        using var response = await Client.GetAsync($"/api/rooms/{Guid.NewGuid()}/labels");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_scope_chosen_at_upload_limits_what_is_found()
    {
        var room = await WaitForStatusAsync((await UploadAsync("ObjectsOnly")).Id, "Ready");

        Assert.Equal("ObjectsOnly", room.Scope);
        Assert.DoesNotContain(room.Objects, o => o.GetProperty("category").GetString() is "Wall" or "Floor" or "Ceiling" or "Opening");
        Assert.Contains(room.Objects, o => o.GetProperty("label").GetString() == "sofa");
    }

    [Fact]
    public async Task The_api_says_which_ai_uploads_can_use()
    {
        var ai = await Client.GetFromJsonAsync<JsonElement>("/api/ai");

        Assert.Equal("Fake", ai.GetProperty("default").GetString());
        Assert.False(ai.GetProperty("realAvailable").GetBoolean()); // tests never have keys
    }

    [Fact]
    public async Task Choosing_the_real_ai_without_keys_is_refused()
    {
        using var response = await PostPhotoAsync(null, ai: "Real");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("isn't set up", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_scope_is_refused()
    {
        using var response = await PostPhotoAsync("Kitchen");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_outline_fix_creates_a_new_label_map_version_and_undo_switches_back()
    {
        var room = await WaitForStatusAsync((await UploadAsync()).Id, "Ready");
        var sofa = room.Objects.Single(o => o.GetProperty("label").GetString() == "sofa").GetProperty("index").GetInt32();
        var before = await LabelsAsync(room.LabelMapUrl!);
        var spot = Array.FindIndex(before, l => l != sofa);
        var (x, y) = ((spot % 1200 + 0.5) / 1200, (spot / 1200 + 0.5) / 800);

        using var fix = await Client.PostAsJsonAsync($"/api/rooms/{room.Id}/outline",
            new { baseRevision = 0, objectIndex = sofa, mode = "Paint", radius = 2, points = new[] { new { x, y } } });
        fix.EnsureSuccessStatusCode();
        var fixedRoom = (await fix.Content.ReadFromJsonAsync<RoomDto>())!;

        Assert.Equal(1, fixedRoom.LabelMapRevision);
        Assert.Equal($"/api/rooms/{room.Id}/labels/1", fixedRoom.LabelMapUrl);
        Assert.Equal(sofa, (await LabelsAsync(fixedRoom.LabelMapUrl!))[spot]);
        Assert.Equal(before, await LabelsAsync($"/api/rooms/{room.Id}/labels/0"));

        using var undo = await Client.PutAsJsonAsync($"/api/rooms/{room.Id}/labels/current", new { revision = 0 });
        undo.EnsureSuccessStatusCode();
        Assert.Equal(0, (await Client.GetFromJsonAsync<RoomDto>($"/api/rooms/{room.Id}"))!.LabelMapRevision);

        using var again = await Client.PostAsJsonAsync($"/api/rooms/{room.Id}/outline",
            new { baseRevision = 0, objectIndex = sofa, mode = "Erase", radius = 0.5, points = new[] { new { x, y } } });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode); // that spot isn't the sofa's in version 0
        using var missing = await Client.GetAsync($"/api/rooms/{room.Id}/labels/7");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task A_hand_marked_object_is_added_as_a_new_label_map_version()
    {
        var room = await WaitForStatusAsync((await UploadAsync()).Id, "Ready");
        var next = room.Objects.Max(o => o.GetProperty("index").GetInt32()) + 1;
        var (x, y) = ((300 + 0.5) / 1200, (200 + 0.5) / 800);

        using var add = await Client.PostAsJsonAsync($"/api/rooms/{room.Id}/objects", new
        {
            baseRevision = 0, label = "side lamp", category = "Lighting",
            strokes = new[] { new { mode = "Paint", radius = 6, points = new[] { new { x, y } } } },
        });
        add.EnsureSuccessStatusCode();
        var withLamp = (await add.Content.ReadFromJsonAsync<RoomDto>())!;

        Assert.Equal(1, withLamp.LabelMapRevision);
        var lamp = withLamp.Objects.Single(o => o.GetProperty("index").GetInt32() == next);
        Assert.Equal(("side lamp", "Lighting"), (lamp.GetProperty("label").GetString(), lamp.GetProperty("category").GetString()));
        Assert.Matches("^#[0-9A-F]{6}$", lamp.GetProperty("dominantColor").GetString());
        Assert.Equal(next, (await LabelsAsync(withLamp.LabelMapUrl!))[200 * 1200 + 300]);

        using var unnamed = await Client.PostAsJsonAsync($"/api/rooms/{room.Id}/objects", new
        {
            baseRevision = 1, label = " ", category = "Other",
            strokes = new[] { new { mode = "Paint", radius = 6, points = new[] { new { x, y } } } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, unnamed.StatusCode);
        using var unknown = await Client.PostAsJsonAsync($"/api/rooms/{Guid.NewGuid()}/objects", new
        {
            baseRevision = 0, label = "lamp", category = "Other",
            strokes = new[] { new { mode = "Paint", radius = 6, points = new[] { new { x, y } } } },
        });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Progress_arrives_live_over_signalr()
    {
        var statuses = new List<string>();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Like the editor: upload first, then connect, so the connection carries the browser's guest cookie.
        var created = await UploadAsync();
        await using var hub = ConnectToHub();
        hub.On<JsonElement>("RoomChanged", room =>
        {
            var status = room.GetProperty("status").GetString()!;
            lock (statuses)
                statuses.Add(status);
            if (status is "Ready" or "Failed")
                finished.TrySetResult();
        });
        await hub.StartAsync();

        var current = await hub.InvokeAsync<JsonElement>("Watch", created.Id);
        if (current.GetProperty("status").GetString() == "Ready")
            finished.TrySetResult(); // finished before we started watching; Watch returned the final state
        await finished.Task.WaitAsync(Timeout);

        Assert.DoesNotContain("Failed", statuses);
    }

    [Fact]
    public async Task Watching_a_missing_room_is_refused()
    {
        await using var hub = ConnectToHub();
        await hub.StartAsync();

        var error = await Assert.ThrowsAsync<HubException>(() => hub.InvokeAsync<JsonElement>("Watch", Guid.NewGuid()));
        Assert.Contains("couldn't find", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A SignalR connection to the test server's room hub.</summary>
    private HubConnection ConnectToHub()
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(factory.ClientOptions.BaseAddress, "/api/hubs/rooms"), options =>
            {
                // The browser's cookies go along, so the hub knows whose rooms it may show.
                options.HttpMessageHandlerFactory = _ => new CookieContainerHandler(_cookies) { InnerHandler = factory.Server.CreateHandler() };
                options.Transports = HttpTransportType.LongPolling; // the test server has no WebSockets
            })
            .Build();
    }

    /// <summary>Uploads a 1200x800 test photo.</summary>
    /// <param name="scope">The optional scope form field.</param>
    private async Task<RoomDto> UploadAsync(string? scope = null)
    {
        using var response = await PostPhotoAsync(scope);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoomDto>())!;
    }

    /// <summary>Posts a 1200x800 test photo.</summary>
    /// <param name="scope">The optional scope form field.</param>
    /// <param name="ai">The optional ai form field.</param>
    private async Task<HttpResponseMessage> PostPhotoAsync(string? scope, string? ai = null)
    {
        using var content = new ByteArrayContent(TestImages.Create(1200, 800));
        content.Headers.ContentType = new MediaTypeHeaderValue(DesignApp.Application.Constants.ContentTypes.Jpeg);
        using var form = new MultipartFormDataContent { { content, "photo", "room.jpg" } };
        if (scope is not null)
            form.Add(new StringContent(scope), "scope");
        if (ai is not null)
            form.Add(new StringContent(ai), "ai");
        return await Client.PostAsync("/api/rooms", form);
    }

    /// <summary>Downloads a label map and decodes it to one byte per pixel.</summary>
    /// <param name="url">The label map's URL.</param>
    private async Task<byte[]> LabelsAsync(string url)
    {
        using var codec = SKCodec.Create(new MemoryStream(await Client.GetByteArrayAsync(url)));
        using var labels = SKBitmap.Decode(codec, new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Gray8, SKAlphaType.Opaque));
        return labels.GetPixelSpan().ToArray();
    }

    /// <summary>Polls the room until it reaches a status.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="status">The status to wait for.</param>
    /// <exception cref="TimeoutException">The room didn't get there in time.</exception>
    private async Task<RoomDto> WaitForStatusAsync(Guid id, string status)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (true)
        {
            var room = (await Client.GetFromJsonAsync<RoomDto>($"/api/rooms/{id}"))!;
            if (room.Status == status)
                return room;
            if (room.Status == "Failed" || DateTime.UtcNow > deadline)
                throw new TimeoutException($"Room {id} is {room.Status} ({room.FailureReason}), expected {status}.");
            await Task.Delay(100);
        }
    }

    /// <summary>The room JSON as a client reads it.</summary>
    /// <param name="Id">The room's id.</param>
    /// <param name="Status">Its analysis status.</param>
    /// <param name="FailureReason">Why it failed, if it did.</param>
    /// <param name="Objects">The detected objects.</param>
    /// <param name="LabelMapUrl">Where the label map is, once ready.</param>
    /// <param name="LabelMapRevision">The current label map version.</param>
    /// <param name="Scope">What the analysis looked for.</param>
    private sealed record RoomDto(Guid Id, string Status, string? FailureReason, List<JsonElement> Objects, string? LabelMapUrl,
        int LabelMapRevision, string Scope, string AiMode);
}
