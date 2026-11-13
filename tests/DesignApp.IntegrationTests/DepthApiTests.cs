using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DesignApp.Tests;

namespace DesignApp.IntegrationTests;

/// <summary>
/// The 3D view's depth map over HTTP: measured once on request (here by the fake), stored and served, reused after
/// that, and private to the room's owner.
/// </summary>
/// <param name="factory">The API running against test containers.</param>
public class DepthApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task A_room_gets_its_depth_map_once_and_only_its_owner_sees_it()
    {
        using var owner = factory.CreateClient();
        var id = await ReadyRoomAsync(owner);
        var before = await owner.GetFromJsonAsync<JsonElement>($"/api/rooms/{id}");
        Assert.Equal(JsonValueKind.Null, before.GetProperty("depthUrl").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/rooms/{id}/depth")).StatusCode);

        using var measured = await owner.PostAsync($"/api/rooms/{id}/depth", null);

        Assert.Equal(HttpStatusCode.OK, measured.StatusCode);
        var room = await measured.Content.ReadFromJsonAsync<JsonElement>();
        var depthUrl = room.GetProperty("depthUrl").GetString();
        Assert.Equal($"/api/rooms/{id}/depth", depthUrl);
        Assert.InRange(room.GetProperty("depthFovX").GetDouble(), 1, 179);
        using var depth = await owner.GetAsync(depthUrl);
        Assert.Equal(HttpStatusCode.OK, depth.StatusCode);
        Assert.Equal("image/png", depth.Content.Headers.ContentType?.MediaType);
        Assert.Contains("immutable", depth.Headers.CacheControl?.ToString(), StringComparison.Ordinal);

        using var again = await owner.PostAsync($"/api/rooms/{id}/depth", null);
        Assert.Equal(depthUrl, (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("depthUrl").GetString());

        using var stranger = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"/api/rooms/{id}/depth", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(depthUrl)).StatusCode);
    }

    /// <summary>Uploads a photo as the browser and waits until the fake AI has analyzed it.</summary>
    /// <param name="browser">The client, with its own cookies.</param>
    /// <returns>The room's id.</returns>
    private static async Task<Guid> ReadyRoomAsync(HttpClient browser)
    {
        using var content = new ByteArrayContent(TestImages.Create(1200, 800));
        content.Headers.ContentType = new MediaTypeHeaderValue(DesignApp.Application.Constants.ContentTypes.Jpeg);
        using var form = new MultipartFormDataContent { { content, "photo", "room.jpg" } };
        using var upload = await browser.PostAsync("/api/rooms", form);
        upload.EnsureSuccessStatusCode();
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while ((await browser.GetFromJsonAsync<JsonElement>($"/api/rooms/{id}")).GetProperty("status").GetString() != "Ready")
        {
            Assert.True(DateTime.UtcNow < deadline, "The room wasn't analyzed within 30 seconds.");
            await Task.Delay(100);
        }
        return id;
    }
}
