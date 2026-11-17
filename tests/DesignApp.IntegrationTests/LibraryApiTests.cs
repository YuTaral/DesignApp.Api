using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DesignApp.IntegrationTests;

/// <summary>
/// The built-in library over HTTP: the designs are listed with their layouts' photos, a layout is copied Ready at once (no
/// analysis), a whole house becomes a design for an account only, and the export tool exists in Development only.
/// </summary>
/// <param name="factory">The API running against test containers.</param>
public class LibraryApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct horse battery";

    [Fact]
    public async Task The_library_lists_its_designs_with_photos()
    {
        using var browser = factory.CreateClient();
        var design = await FirstDesignAsync(browser);

        Assert.False(string.IsNullOrWhiteSpace(design.Name));
        using var cover = await browser.GetAsync(design.CoverUrl);

        Assert.Equal(HttpStatusCode.OK, cover.StatusCode);
        Assert.Equal(DesignApp.Application.Constants.ContentTypes.Jpeg, cover.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/api/library/no-such-design/cover")).StatusCode);
        var room = Assert.IsType<LibraryRoomDto>(design.Rooms[0]);
        var layout = Assert.IsType<LibraryLayoutDto>(room.Layouts[0]);

        Assert.True(layout.ObjectCount > 0);
        using var photo = await browser.GetAsync(layout.PhotoUrl);

        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal(DesignApp.Application.Constants.ContentTypes.Jpeg, photo.Content.Headers.ContentType?.MediaType);
        Assert.True(photo.Headers.CacheControl?.Public);
        using var thumbnail = await browser.GetAsync(layout.ThumbnailUrl);

        Assert.Equal(HttpStatusCode.OK, thumbnail.StatusCode);
        Assert.Equal(DesignApp.Application.Constants.ContentTypes.Jpeg, thumbnail.Content.Headers.ContentType?.MediaType);
        Assert.True(thumbnail.Content.Headers.ContentLength < photo.Content.Headers.ContentLength / 4, "The thumbnail is much smaller than the photo.");
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/library/{design.Id}/{room.Id}/no-such-layout/photo")).StatusCode);
    }

    [Fact]
    public async Task A_guest_copies_a_layout_that_is_ready_at_once_with_its_outlines()
    {
        using var guest = factory.CreateClient();
        var design = await FirstDesignAsync(guest);
        var source = design.Rooms[0];
        var layout = source.Layouts[^1];

        using var copied = await guest.PostAsync($"/api/library/{design.Id}/{source.Id}/{layout.Id}/copy", null);

        Assert.Equal(HttpStatusCode.Created, copied.StatusCode);
        var room = await copied.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Ready", room.GetProperty("status").GetString());
        Assert.Equal($"{design.Id}/{source.Id}/{layout.Id}", room.GetProperty("libraryRoom").GetString());
        Assert.Equal(layout.ObjectCount, room.GetProperty("objects").GetArrayLength());
        Assert.Equal(layout.Width, room.GetProperty("width").GetInt32());

        // The copy is the guest's own room: its files are served like an upload's.
        var id = room.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync(room.GetProperty("photoUrl").GetString())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync(room.GetProperty("labelMapUrl").GetString())).StatusCode);
        using var other = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/rooms/{id}")).StatusCode);
    }

    [Fact]
    public async Task Unknown_layouts_rooms_and_designs_are_not_found()
    {
        using var browser = factory.CreateClient();
        var design = await FirstDesignAsync(browser);
        var room = design.Rooms[0];

        Assert.Equal(HttpStatusCode.NotFound, (await browser.PostAsync($"/api/library/{design.Id}/{room.Id}/no-such-layout/copy", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.PostAsync($"/api/library/{design.Id}/no-such-room/1/copy", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.PostAsync("/api/library/no-such-design/kitchen/1/copy", null)).StatusCode);
    }

    [Fact]
    public async Task An_unknown_ai_is_refused()
    {
        using var browser = factory.CreateClient();
        var design = await FirstDesignAsync(browser);

        using var refused = await browser.PostAsync($"/api/library/{design.Id}/{design.Rooms[0].Id}/{design.Rooms[0].Layouts[0].Id}/copy?ai=Magic", null);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
    }

    [Fact]
    public async Task A_whole_house_becomes_a_design_for_an_account_only()
    {
        using var guest = factory.CreateClient();
        var design = await FirstDesignAsync(guest);

        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsync($"/api/library/{design.Id}/copy", null)).StatusCode);

        using var account = factory.CreateClient();

        (await account.PostAsJsonAsync("/api/account/sign-up", new { email = $"library-{Guid.NewGuid():N}@example.com", password = Password })).EnsureSuccessStatusCode();
        using var copied = await account.PostAsync($"/api/library/{design.Id}/copy", null);

        Assert.Equal(HttpStatusCode.Created, copied.StatusCode);
        var saved = await copied.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(design.Name, saved.GetProperty("name").GetString());
        var rooms = saved.GetProperty("rooms").EnumerateArray().Select(r => r.GetProperty("roomId").GetGuid()).ToList();

        Assert.Equal(design.Rooms.Count, rooms.Count);

        foreach (var (id, source) in rooms.Zip(design.Rooms))
        {
            var room = await account.GetFromJsonAsync<JsonElement>($"/api/rooms/{id}");

            Assert.Equal("Ready", room.GetProperty("status").GetString());
            // A whole house uses each room's first layout.
            Assert.Equal($"{design.Id}/{source.Id}/{source.Layouts[0].Id}", room.GetProperty("libraryRoom").GetString());
        }

        var designs = await account.GetFromJsonAsync<JsonElement>("/api/designs");

        Assert.Equal(1, designs.GetArrayLength());
    }

    [Fact]
    public async Task The_export_tool_is_not_there_outside_development()
    {
        using var browser = factory.CreateClient();
        var design = await FirstDesignAsync(browser);
        using var copied = await browser.PostAsync($"/api/library/{design.Id}/{design.Rooms[0].Id}/{design.Rooms[0].Layouts[0].Id}/copy", null);
        var id = (await copied.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/dev/library-export/{id}?name=kitchen")).StatusCode);
    }

    /// <summary>The first library design, which every test uses.</summary>
    /// <param name="browser">The client.</param>
    private static async Task<LibraryDesignDto> FirstDesignAsync(HttpClient browser)
    {
        var designs = await browser.GetFromJsonAsync<List<LibraryDesignDto>>("/api/library");

        Assert.NotNull(designs);

        return Assert.IsType<LibraryDesignDto>(designs.FirstOrDefault(), exactMatch: false);
    }

    /// <summary>A library design as a client reads it.</summary>
    private sealed record LibraryDesignDto(string Id, string Name, string Style, string Description, string CoverUrl, List<LibraryRoomDto> Rooms);

    /// <summary>A library room as a client reads it.</summary>
    private sealed record LibraryRoomDto(string Id, string Title, List<LibraryLayoutDto> Layouts);

    /// <summary>A layout of a library room as a client reads it.</summary>
    private sealed record LibraryLayoutDto(string Id, string PhotoUrl, string ThumbnailUrl, int Width, int Height, int ObjectCount);
}
