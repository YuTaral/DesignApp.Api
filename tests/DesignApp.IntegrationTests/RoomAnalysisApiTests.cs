using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DesignApp.Tests;
using Microsoft.AspNetCore.Http.Connections;
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
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task An_uploaded_room_is_analyzed_in_the_background()
    {
        var created = await UploadAsync();

        var room = await WaitForStatusAsync(created.Id, "Ready");

        Assert.NotEmpty(room.Objects);
        Assert.Equal($"/api/rooms/{room.Id}/labels", room.LabelMapUrl);
        var sofa = room.Objects.Single(o => o.GetProperty("label").GetString() == "sofa");
        Assert.Equal("Furniture", sofa.GetProperty("category").GetString());
        Assert.Matches("^#[0-9A-F]{6}$", sofa.GetProperty("dominantColor").GetString());
    }

    [Fact]
    public async Task The_label_map_holds_an_object_index_per_pixel()
    {
        var room = await WaitForStatusAsync((await UploadAsync()).Id, "Ready");

        using var response = await _client.GetAsync(room.LabelMapUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        using var codec = SKCodec.Create(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        Assert.Equal((1200, 800), (codec.Info.Width, codec.Info.Height));
        using var labels = SKBitmap.Decode(codec, new SKImageInfo(1200, 800, SKColorType.Gray8, SKAlphaType.Opaque));
        var indexes = labels.GetPixelSpan().ToArray().Where(v => v != 0).Distinct().Order();
        Assert.Equal(room.Objects.Select(o => (byte)o.GetProperty("index").GetInt32()).Order(), indexes);
    }

    [Fact]
    public async Task There_is_no_label_map_for_an_unknown_room()
    {
        using var response = await _client.GetAsync($"/api/rooms/{Guid.NewGuid()}/labels");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Progress_arrives_live_over_signalr()
    {
        var statuses = new List<string>();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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

        var created = await UploadAsync();
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
    private HubConnection ConnectToHub() => new HubConnectionBuilder()
        .WithUrl(new Uri(factory.Server.BaseAddress, "/api/hubs/rooms"), options =>
        {
            options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            options.Transports = HttpTransportType.LongPolling; // the test server has no WebSockets
        })
        .Build();

    /// <summary>Uploads a 1200x800 test photo.</summary>
    private async Task<RoomDto> UploadAsync()
    {
        using var content = new ByteArrayContent(TestImages.Create(1200, 800));
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        using var form = new MultipartFormDataContent { { content, "photo", "room.jpg" } };
        using var response = await _client.PostAsync("/api/rooms", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoomDto>())!;
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
            var room = (await _client.GetFromJsonAsync<RoomDto>($"/api/rooms/{id}"))!;
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
    private sealed record RoomDto(Guid Id, string Status, string? FailureReason, List<JsonElement> Objects, string? LabelMapUrl);
}
