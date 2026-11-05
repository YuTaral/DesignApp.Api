using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DesignApp.Tests;

namespace DesignApp.IntegrationTests;

/// <summary>
/// End-to-end tests of saved designs over HTTP: an account saves, lists, opens, changes and
/// deletes them; guests and other accounts can't; invalid recolors are refused.
/// </summary>
/// <param name="factory">The API running against test containers.</param>
public class DesignApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct horse battery";

    [Fact]
    public async Task Guests_must_sign_in_to_save_or_see_designs()
    {
        using var guest = factory.CreateClient();
        var room = await ReadyRoomAsync(guest);

        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/designs")).StatusCode);
        using var save = await guest.PostAsJsonAsync("/api/designs", new { roomId = room.Id, name = "Mine", edits = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Unauthorized, save.StatusCode);
    }

    [Fact]
    public async Task An_account_saves_lists_opens_changes_and_deletes_a_design()
    {
        using var browser = await SignedUpAsync();
        var room = await ReadyRoomAsync(browser);
        var (first, second) = (room.Objects[0], room.Objects[1]);

        using var created = await browser.PostAsJsonAsync("/api/designs", new
        {
            roomId = room.Id,
            name = "  Sage walls  ",
            edits = new[] { new { objectIndex = first, color = "#8fa98b", strength = 0.8 } },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var design = (await created.Content.ReadFromJsonAsync<DesignDto>())!;
        Assert.Equal(("Sage walls", room.Id, $"/api/rooms/{room.Id}/photo"), (design.Name, design.RoomId, design.PhotoUrl));
        Assert.Equal([new EditDto(first, "#8FA98B", 0.8)], design.Edits);
        Assert.Equal($"/api/designs/{design.Id}", created.Headers.Location?.OriginalString);

        using var updated = await browser.PutAsJsonAsync($"/api/designs/{design.Id}", new
        {
            name = "Sage and oak",
            edits = new[] { new { objectIndex = first, color = "#8FA98B", strength = 0.5 }, new { objectIndex = second, color = "#A0522D", strength = 1.0 } },
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var opened = await browser.GetFromJsonAsync<DesignDto>($"/api/designs/{design.Id}");
        Assert.Equal("Sage and oak", opened!.Name);
        Assert.Equal([new EditDto(first, "#8FA98B", 0.5), new EditDto(second, "#A0522D", 1.0)], opened.Edits);
        Assert.True(opened.UpdatedAt >= design.UpdatedAt);
        Assert.Equal([design.Id], (await browser.GetFromJsonAsync<List<DesignDto>>("/api/designs"))!.Select(d => d.Id));

        Assert.Equal(HttpStatusCode.NoContent, (await browser.DeleteAsync($"/api/designs/{design.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/designs/{design.Id}")).StatusCode);
        Assert.Empty((await browser.GetFromJsonAsync<List<DesignDto>>("/api/designs"))!);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/api/rooms/{room.Id}")).StatusCode); // the room stays
    }

    [Fact]
    public async Task Another_account_can_not_touch_a_design_or_design_someone_elses_room()
    {
        using var owner = await SignedUpAsync();
        var room = await ReadyRoomAsync(owner);
        using var created = await owner.PostAsJsonAsync("/api/designs", new { roomId = room.Id, name = "Mine", edits = Array.Empty<object>() });
        var design = (await created.Content.ReadFromJsonAsync<DesignDto>())!;

        using var other = await SignedUpAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/designs/{design.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/designs/{design.Id}", new { name = "Theirs" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/designs/{design.Id}")).StatusCode);
        Assert.Empty((await other.GetFromJsonAsync<List<DesignDto>>("/api/designs"))!);
        using var copy = await other.PostAsJsonAsync("/api/designs", new { roomId = room.Id, name = "Copy", edits = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.NotFound, copy.StatusCode);
        Assert.Equal("Mine", (await owner.GetFromJsonAsync<DesignDto>($"/api/designs/{design.Id}"))!.Name);
    }

    [Fact]
    public async Task A_room_made_as_a_guest_can_be_designed_after_signing_up()
    {
        using var browser = factory.CreateClient();
        var room = await ReadyRoomAsync(browser);
        (await browser.PostAsJsonAsync("/api/account/sign-up", new { email = NewEmail(), password = Password })).EnsureSuccessStatusCode();

        using var created = await browser.PostAsJsonAsync("/api/designs", new { roomId = room.Id, name = "From my guest visit", edits = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task Invalid_names_and_recolors_are_refused()
    {
        using var browser = await SignedUpAsync();
        var room = await ReadyRoomAsync(browser);
        var index = room.Objects[0];

        async Task<string?> Refused(object body)
        {
            using var response = await browser.PostAsJsonAsync("/api/designs", body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString();
        }

        Assert.Equal("Design name must be 1 to 80 characters.", await Refused(new { roomId = room.Id, name = "  ", edits = Array.Empty<object>() }));
        Assert.Equal("Object 250 does not exist in this room.",
            await Refused(new { roomId = room.Id, name = "x", edits = new[] { new { objectIndex = 250, color = "#000000", strength = 1.0 } } }));
        Assert.Equal("'red' is not a #RRGGBB color.",
            await Refused(new { roomId = room.Id, name = "x", edits = new[] { new { objectIndex = index, color = "red", strength = 1.0 } } }));
        Assert.Equal("Recolor strength must be between 0 and 1.",
            await Refused(new { roomId = room.Id, name = "x", edits = new[] { new { objectIndex = index, color = "#000000", strength = 1.5 } } }));
        Assert.Equal($"Object {index} has more than one edit.",
            await Refused(new { roomId = room.Id, name = "x", edits = new[] { new { objectIndex = index, color = "#000000", strength = 1.0 }, new { objectIndex = index, color = "#FFFFFF", strength = 1.0 } } }));
    }

    /// <summary>A fresh email, so tests sharing the database don't collide.</summary>
    private static string NewEmail() => $"designer-{Guid.NewGuid():N}@example.com";

    /// <summary>A browser signed in to a new account.</summary>
    private async Task<HttpClient> SignedUpAsync()
    {
        var browser = factory.CreateClient();
        (await browser.PostAsJsonAsync("/api/account/sign-up", new { email = NewEmail(), password = Password })).EnsureSuccessStatusCode();
        return browser;
    }

    /// <summary>Uploads a photo as the browser and waits until the fake AI has analyzed it.</summary>
    /// <param name="browser">The client, with its own cookies.</param>
    /// <returns>The room's id and its objects' indexes.</returns>
    private static async Task<ReadyRoom> ReadyRoomAsync(HttpClient browser)
    {
        using var content = new ByteArrayContent(TestImages.Create(1200, 800));
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        using var form = new MultipartFormDataContent { { content, "photo", "room.jpg" } };
        using var upload = await browser.PostAsync("/api/rooms", form);
        upload.EnsureSuccessStatusCode();
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var room = await browser.GetFromJsonAsync<JsonElement>($"/api/rooms/{id}");
            var status = room.GetProperty("status").GetString();
            if (status == "Ready")
                return new ReadyRoom(id, [.. room.GetProperty("objects").EnumerateArray().Select(o => o.GetProperty("index").GetInt32())]);
            Assert.NotEqual("Failed", status);
            Assert.True(DateTime.UtcNow < deadline, $"The room was still {status} after 30 seconds.");
            await Task.Delay(100);
        }
    }

    /// <summary>An analyzed room.</summary>
    /// <param name="Id">The room's id.</param>
    /// <param name="Objects">Its objects' indexes.</param>
    private sealed record ReadyRoom(Guid Id, List<int> Objects);

    /// <summary>A design as a client reads it.</summary>
    private sealed record DesignDto(Guid Id, Guid RoomId, string Name, DateTimeOffset UpdatedAt, string PhotoUrl, List<EditDto> Edits);

    /// <summary>One recolor as a client reads it.</summary>
    private sealed record EditDto(int ObjectIndex, string Color, double Strength);
}
