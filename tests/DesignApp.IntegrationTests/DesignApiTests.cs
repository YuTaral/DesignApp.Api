using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DesignApp.Application.Rooms;
using DesignApp.Domain.Rooms;
using DesignApp.Tests;
using Microsoft.Extensions.DependencyInjection;

namespace DesignApp.IntegrationTests;

/// <summary>
/// End-to-end tests of designs over HTTP: a design has a name, a description and one or more rooms
/// with their own colors; rooms are only kept inside a design; guests and other accounts can't
/// touch them; invalid input is refused; unsaved uploads are deleted after a day.
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
        using var save = await guest.PostAsJsonAsync("/api/designs", new { roomId = room.Id, name = "Mine" });
        Assert.Equal(HttpStatusCode.Unauthorized, save.StatusCode);
    }

    [Fact]
    public async Task A_design_is_created_described_given_more_rooms_and_colored_per_room()
    {
        using var browser = await SignedUpAsync();
        var kitchen = await ReadyRoomAsync(browser);
        var bedroom = await ReadyRoomAsync(browser);

        using var created = await browser.PostAsJsonAsync("/api/designs", new
        {
            roomId = kitchen.Id,
            name = "  Flat makeover  ",
            description = "  Calm greens  ",
            edits = new[] { new { objectIndex = kitchen.Objects[0], color = "#8fa98b", strength = 0.8 } },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var design = (await created.Content.ReadFromJsonAsync<DesignDto>())!;
        Assert.Equal(("Flat makeover", "Calm greens"), (design.Name, design.Description));
        var first = Assert.Single(design.Rooms);
        Assert.Equal((kitchen.Id, $"/api/rooms/{kitchen.Id}/photo"), (first.RoomId, first.PhotoUrl));
        Assert.Equal([new EditDto(kitchen.Objects[0], "#8FA98B", 0.8)], first.Edits);

        var added = await OkAsync(await browser.PostAsJsonAsync($"/api/designs/{design.Id}/rooms", new { roomId = bedroom.Id }));
        Assert.Equal([kitchen.Id, bedroom.Id], added.Rooms.Select(r => r.RoomId));
        Assert.Empty(added.Rooms[1].Edits);

        var colored = await OkAsync(await browser.PutAsJsonAsync($"/api/designs/{design.Id}/rooms/{bedroom.Id}", new
        {
            edits = new[] { new { objectIndex = bedroom.Objects[1], color = "#A0522D", strength = 1.0 } },
        }));
        Assert.Equal([new EditDto(bedroom.Objects[1], "#A0522D", 1.0)], colored.Rooms[1].Edits);
        Assert.Single(colored.Rooms[0].Edits); // the other room keeps its colors

        var renamed = await OkAsync(await browser.PutAsJsonAsync($"/api/designs/{design.Id}", new { name = "Whole flat", description = "" }));
        Assert.Equal(("Whole flat", ""), (renamed.Name, renamed.Description));

        var listed = Assert.Single((await browser.GetFromJsonAsync<List<DesignDto>>("/api/designs"))!);
        Assert.Equal((design.Id, 2), (listed.Id, listed.Rooms.Count));
    }

    [Fact]
    public async Task Removing_a_room_deletes_it_and_the_last_room_stays()
    {
        using var browser = await SignedUpAsync();
        var kitchen = await ReadyRoomAsync(browser);
        var bedroom = await ReadyRoomAsync(browser);
        var design = await CreateAsync(browser, kitchen.Id, "Flat");
        await OkAsync(await browser.PostAsJsonAsync($"/api/designs/{design.Id}/rooms", new { roomId = bedroom.Id }));

        var left = await OkAsync(await browser.DeleteAsync($"/api/designs/{design.Id}/rooms/{kitchen.Id}"));

        Assert.Equal([bedroom.Id], left.Rooms.Select(r => r.RoomId));
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/rooms/{kitchen.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/rooms/{kitchen.Id}/photo")).StatusCode);
        Assert.Equal("A design needs at least one room. Delete the design instead.",
            await ProblemAsync(await browser.DeleteAsync($"/api/designs/{design.Id}/rooms/{bedroom.Id}")));
    }

    [Fact]
    public async Task Deleting_a_design_deletes_its_rooms()
    {
        using var browser = await SignedUpAsync();
        var kitchen = await ReadyRoomAsync(browser);
        var design = await CreateAsync(browser, kitchen.Id, "Short-lived");

        Assert.Equal(HttpStatusCode.NoContent, (await browser.DeleteAsync($"/api/designs/{design.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/designs/{design.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/rooms/{kitchen.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_room_belongs_to_one_design_only()
    {
        using var browser = await SignedUpAsync();
        var kitchen = await ReadyRoomAsync(browser);
        var design = await CreateAsync(browser, kitchen.Id, "First");

        Assert.Equal("This room is already in a design.",
            await ProblemAsync(await browser.PostAsJsonAsync("/api/designs", new { roomId = kitchen.Id, name = "Second" })));
        Assert.Equal("This room is already in a design.",
            await ProblemAsync(await browser.PostAsJsonAsync($"/api/designs/{design.Id}/rooms", new { roomId = kitchen.Id })));
    }

    [Fact]
    public async Task Another_account_can_not_touch_a_design_or_use_someone_elses_room()
    {
        using var owner = await SignedUpAsync();
        var room = await ReadyRoomAsync(owner);
        var design = await CreateAsync(owner, room.Id, "Mine");

        using var other = await SignedUpAsync();
        var theirRoom = await ReadyRoomAsync(other);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/designs/{design.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/designs/{design.Id}", new { name = "Theirs" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/designs/{design.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/designs/{design.Id}/rooms", new { roomId = theirRoom.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/designs/{design.Id}/rooms/{room.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync("/api/designs", new { roomId = room.Id, name = "Copy" })).StatusCode);
        Assert.Empty((await other.GetFromJsonAsync<List<DesignDto>>("/api/designs"))!);
        Assert.Equal("Mine", (await owner.GetFromJsonAsync<DesignDto>($"/api/designs/{design.Id}"))!.Name);
    }

    [Fact]
    public async Task A_room_made_as_a_guest_can_be_saved_after_signing_up()
    {
        using var browser = factory.CreateClient();
        var room = await ReadyRoomAsync(browser);
        (await browser.PostAsJsonAsync("/api/account/sign-up", new { email = NewEmail(), password = Password })).EnsureSuccessStatusCode();

        using var created = await browser.PostAsJsonAsync("/api/designs", new { roomId = room.Id, name = "From my guest visit" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task Invalid_details_and_colors_are_refused()
    {
        using var browser = await SignedUpAsync();
        var room = await ReadyRoomAsync(browser);
        var index = room.Objects[0];

        async Task<string?> Refused(object body) => await ProblemAsync(await browser.PostAsJsonAsync("/api/designs", body));

        Assert.Equal("Design name must be 1 to 80 characters.", await Refused(new { roomId = room.Id, name = "  " }));
        Assert.Equal("The description can be at most 300 characters.", await Refused(new { roomId = room.Id, name = "x", description = new string('d', 301) }));
        Assert.Equal("Object 250 does not exist in this room.",
            await Refused(new { roomId = room.Id, name = "x", edits = new[] { new { objectIndex = 250, color = "#000000", strength = 1.0 } } }));
        Assert.Equal("'red' is not a #RRGGBB color.",
            await Refused(new { roomId = room.Id, name = "x", edits = new[] { new { objectIndex = index, color = "red", strength = 1.0 } } }));
        Assert.Equal("Recolor strength must be between 0 and 1.",
            await Refused(new { roomId = room.Id, name = "x", edits = new[] { new { objectIndex = index, color = "#000000", strength = 1.5 } } }));
        Assert.Equal($"Object {index} has more than one edit.",
            await Refused(new { roomId = room.Id, name = "x", edits = new[] { new { objectIndex = index, color = "#000000", strength = 1.0 }, new { objectIndex = index, color = "#FFFFFF", strength = 1.0 } } }));
    }

    [Fact]
    public async Task Uploads_not_saved_to_a_design_within_a_day_are_deleted()
    {
        using var browser = await SignedUpAsync();
        var saved = await ReadyRoomAsync(browser);
        var unsaved = await ReadyRoomAsync(browser);
        await CreateAsync(browser, saved.Id, "Kept");
        var dayAndAHourAgo = DateTimeOffset.UtcNow.AddHours(-25);
        await AgeAsync(saved.Id, dayAndAHourAgo);
        await AgeAsync(unsaved.Id, dayAndAHourAgo);

        using (var scope = factory.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<DeleteUnsavedRooms>().HandleAsync(CancellationToken.None) >= 1);

        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/rooms/{unsaved.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/rooms/{unsaved.Id}/photo")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/api/rooms/{saved.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/api/rooms/{saved.Id}/photo")).StatusCode);
    }

    /// <summary>Backdates a room's upload time in the database, as if it had been uploaded then.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="uploaded">When it should look uploaded.</param>
    private async Task AgeAsync(Guid roomId, DateTimeOffset uploaded)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DesignApp.Infrastructure.Persistence.AppDbContext>();
        var room = await db.Rooms.FindAsync(roomId);
        db.Entry(room!).Property(nameof(Room.CreatedAt)).CurrentValue = uploaded;
        await db.SaveChangesAsync();
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

    /// <summary>Creates a design with one room and no colors.</summary>
    private static async Task<DesignDto> CreateAsync(HttpClient browser, Guid roomId, string name)
    {
        using var created = await browser.PostAsJsonAsync("/api/designs", new { roomId, name });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<DesignDto>())!;
    }

    /// <summary>Reads a 200 design response.</summary>
    /// <param name="response">The response; disposed here.</param>
    private static async Task<DesignDto> OkAsync(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<DesignDto>())!;
        }
    }

    /// <summary>The detail of a refused request, checking it was refused with 400.</summary>
    /// <param name="response">The response; disposed here.</param>
    private static async Task<string?> ProblemAsync(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString();
        }
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
    private sealed record DesignDto(Guid Id, string Name, string Description, DateTimeOffset UpdatedAt, List<DesignRoomDto> Rooms);

    /// <summary>One room of a design as a client reads it.</summary>
    private sealed record DesignRoomDto(Guid RoomId, string PhotoUrl, List<EditDto> Edits);

    /// <summary>One recolor as a client reads it.</summary>
    private sealed record EditDto(int ObjectIndex, string Color, double Strength);
}
