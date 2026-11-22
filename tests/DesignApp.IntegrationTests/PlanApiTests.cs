using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DesignApp.Application.UseCases;
using DesignApp.Tests;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace DesignApp.IntegrationTests;

/// <summary>The room builder over HTTP: the catalog, building a room, describing an object and generating a photo.</summary>
/// <param name="factory">The API with a real database.</param>
public class PlanApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly CookieContainer _cookies = new();

    private HttpClient Client => field ??=
        factory.CreateDefaultClient(factory.ClientOptions.BaseAddress, new CookieContainerHandler(_cookies));

    [Fact]
    public async Task Each_room_type_has_windows_doors_and_common_objects_in_three_variations_and_their_materials()
    {
        using var response = await Client.GetAsync("/api/builder/catalog/bedroom");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var objects = (await response.Content.ReadFromJsonAsync<List<JsonElement>>())!;

        // Every room's eighteen: six windows and doors, nine things to hang and three for the floor; then the bedroom's seventeen.
        Assert.Equal(35, objects.Count);
        Assert.Contains(objects, o => o.GetProperty("key").GetString() == "painting" && o.GetProperty("shape").GetString() == "Picture" && o.GetProperty("elevation").GetDouble() == 130);
        Assert.Contains(objects, o => o.GetProperty("key").GetString() == "window" && o.GetProperty("elevation").GetDouble() == 90);
        Assert.Contains(objects, o => o.GetProperty("key").GetString() == "double-bed" && o.GetProperty("shape").GetString() == "Bed");
        Assert.All(objects, o => Assert.Equal(3, o.GetProperty("variants").GetArrayLength()));
        var desk = objects.Single(o => o.GetProperty("key").GetString() == "desk").GetProperty("variants").EnumerateArray().ToList();

        Assert.Equal(("L-shaped", "LShaped"), (desk[2].GetProperty("name").GetString(), desk[2].GetProperty("form").GetString()));
        // Each piece of furniture offers its five most popular decorations; a rug and a lamp offer none.
        var bed = objects.Single(o => o.GetProperty("key").GetString() == "double-bed").GetProperty("decorations").EnumerateArray().Select(d => d.GetString()).ToList();

        Assert.Equal(5, bed.Count);
        Assert.Contains("throw blanket", bed);
        Assert.Equal(0, objects.Single(o => o.GetProperty("key").GetString() == "rug").GetProperty("decorations").GetArrayLength());
        // Each offers what it is usually made of, and starts in one of them.
        var bedFrame = objects.Single(o => o.GetProperty("key").GetString() == "double-bed");

        Assert.Contains("oak", bedFrame.GetProperty("materials").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal("linen upholstered", bedFrame.GetProperty("material").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/builder/catalog/garage")).StatusCode);
    }

    [Fact]
    public async Task A_room_is_built_saved_and_read_back_by_its_owner_only()
    {
        using var created = await Client.PostAsJsonAsync("/api/plans",
            new { name = "Lounge", roomType = "LivingRoom", width = 4.0, length = 5.0, height = 2.6, wallColor = "#E8E2D6", floorColor = "#B89B7A" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var plan = (await created.Content.ReadFromJsonAsync<JsonElement>())!;
        var id = plan.GetProperty("id").GetGuid();

        Assert.Equal(("Lounge", "Fake"), (plan.GetProperty("name").GetString(), plan.GetProperty("aiMode").GetString()));
        // A new room starts furnished for its type and size: a door, windows and the living room's furniture.
        var starter = plan.GetProperty("items").EnumerateArray().ToList();

        Assert.Contains(starter, i => i.GetProperty("shape").GetString() == "Door" && i.GetProperty("wall").GetInt32() == 3);
        Assert.Contains(starter, i => i.GetProperty("label").GetString() == "three-seat sofa");

        using var saved = await Client.PutAsJsonAsync($"/api/plans/{id}", new
        {
            name = "Lounge", width = 4.2, length = 5.0, height = 2.6, wallColor = "#E8E2D6", floorColor = "#B89B7A", floorType = "Stone", view = 3,
            items = new object[]
            {
                new { label = "sofa", shape = "Sofa", x = 210.0, y = 60.0, width = 220.0, depth = 95.0, height = 85.0, rotation = 0, color = "#8A8580" },
                new { label = "window", shape = "Window", x = 150.0, y = 0.0, width = 120.0, depth = 10.0, height = 140.0, rotation = 0, color = "#F4F1EA", wall = 1, elevation = 90.0 },
                new { label = "round rug", shape = "Rug", x = 210.0, y = 300.0, width = 200.0, depth = 200.0, height = 1.0, rotation = 0, color = "#D8C3A5", form = "Round" },
            },
        });

        saved.EnsureSuccessStatusCode();
        var read = await Client.GetFromJsonAsync<JsonElement>($"/api/plans/{id}");

        Assert.Equal((4.2, "Stone", 3), (read.GetProperty("width").GetDouble(), read.GetProperty("floorType").GetString(), read.GetProperty("view").GetInt32()));
        // Without a corner the camera looks straight at the wall in view.
        Assert.Equal("None", read.GetProperty("corner").GetString());
        // Without an outline the room is a rectangle of its width and length.
        Assert.Equal("420,0;420,500", string.Join(";", read.GetProperty("outline").EnumerateArray().Skip(1).Take(2).Select(p => $"{p.GetProperty("x").GetDouble()},{p.GetProperty("y").GetDouble()}")));
        var items = read.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(("Sofa", 1, 90.0), (items[0].GetProperty("shape").GetString(), items[1].GetProperty("wall").GetInt32(), items[1].GetProperty("elevation").GetDouble()));
        Assert.Equal(("Standard", "Round"), (items[0].GetProperty("form").GetString(), items[2].GetProperty("form").GetString()));

        // A window that runs past its wall's end is refused.
        using var offTheWall = await Client.PutAsJsonAsync($"/api/plans/{id}", new
        {
            width = 4.2, length = 5.0, height = 2.6, wallColor = "#E8E2D6", floorColor = "#B89B7A", floorType = "Stone", view = 3,
            items = new[] { new { label = "window", shape = "Window", x = 480.0, y = 0.0, width = 120.0, depth = 10.0, height = 140.0, rotation = 0, color = "#F4F1EA", wall = 1, elevation = 90.0 } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, offTheWall.StatusCode);

        // An L-shaped room: its walls are numbered clockwise from the top-left corner, and its size is its outline's.
        var lShape = new[] { new { x = 0.0, y = 0.0 }, new { x = 400.0, y = 0.0 }, new { x = 400.0, y = 300.0 }, new { x = 250.0, y = 300.0 }, new { x = 250.0, y = 500.0 }, new { x = 0.0, y = 500.0 } };
        using var bent = await Client.PutAsJsonAsync($"/api/plans/{id}", new
        {
            width = 1.0, length = 1.0, height = 2.6, wallColor = "#E8E2D6", floorColor = "#B89B7A", floorType = "Stone", view = 3, outline = lShape, corner = "Right",
            items = new[] { new { label = "window", shape = "Window", x = 100.0, y = 0.0, width = 120.0, depth = 10.0, height = 140.0, rotation = 0, color = "#F4F1EA", wall = 3, elevation = 90.0 } },
        });

        bent.EnsureSuccessStatusCode();
        var bentRead = await Client.GetFromJsonAsync<JsonElement>($"/api/plans/{id}");

        Assert.Equal((6, 4.0, 5.0, 3), (bentRead.GetProperty("outline").GetArrayLength(), bentRead.GetProperty("width").GetDouble(), bentRead.GetProperty("length").GetDouble(), bentRead.GetProperty("view").GetInt32()));
        Assert.Equal("Right", bentRead.GetProperty("corner").GetString());

        // Wall 3 is 200 cm long, so a window past its end, a seventh wall or corners running the wrong way are refused.
        foreach (var (outline, wall, view) in new[] { (lShape, 3, 3), (lShape, 6, 0), (lShape.Reverse().ToArray(), 0, 0) })
        {
            using var refused = await Client.PutAsJsonAsync($"/api/plans/{id}", new
            {
                height = 2.6, wallColor = "#E8E2D6", floorColor = "#B89B7A", view, outline,
                items = new[] { new { label = "window", shape = "Window", x = wall == 3 ? 180.0 : 100.0, y = 0.0, width = 120.0, depth = 10.0, height = 140.0, rotation = 0, color = "#F4F1EA", wall, elevation = 90.0 } },
            });

            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        }

        // Things hung on walls: a painting is kept with its height and how far it sticks out; one taller than the room
        // reaches, or one sticking out too far, is refused.
        var painting = new { label = "painting", shape = "Picture", x = 100.0, y = 0.0, width = 80.0, depth = 3.0, height = 60.0, rotation = 0, color = "#2F2C2A", wall = 0, elevation = 130.0 };
        using var hung = await Client.PutAsJsonAsync($"/api/plans/{id}", new { height = 2.6, wallColor = "#E8E2D6", floorColor = "#B89B7A", view = 0, outline = lShape, items = new[] { painting } });

        hung.EnsureSuccessStatusCode();
        var hungRead = (await Client.GetFromJsonAsync<JsonElement>($"/api/plans/{id}")).GetProperty("items")[0];

        Assert.Equal(("Picture", 130.0, 3.0), (hungRead.GetProperty("shape").GetString(), hungRead.GetProperty("elevation").GetDouble(), hungRead.GetProperty("depth").GetDouble()));

        foreach (var bad in new[] { painting with { elevation = 220.0 }, painting with { depth = 80.0 } })
        {
            using var refused = await Client.PutAsJsonAsync($"/api/plans/{id}", new { height = 2.6, wallColor = "#E8E2D6", floorColor = "#B89B7A", view = 0, outline = lShape, items = new[] { bad } });

            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        }

        using var badWalls = await Client.PutAsJsonAsync($"/api/plans/{id}", new { width = 0.5, length = 5.0, height = 2.6, wallColor = "#E8E2D6", floorColor = "#B89B7A" });

        Assert.Equal(HttpStatusCode.BadRequest, badWalls.StatusCode);
        using var badTurn = await Client.PutAsJsonAsync($"/api/plans/{id}", new
        {
            width = 4.0, length = 5.0, height = 2.6, wallColor = "#E8E2D6", floorColor = "#B89B7A",
            items = new[] { new { label = "sofa", shape = "Sofa", x = 0.0, y = 0.0, width = 220.0, depth = 95.0, height = 85.0, rotation = 45, color = "#8A8580" } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, badTurn.StatusCode);
        using var roundSofa = await Client.PutAsJsonAsync($"/api/plans/{id}", new
        {
            width = 4.0, length = 5.0, height = 2.6, wallColor = "#E8E2D6", floorColor = "#B89B7A",
            items = new[] { new { label = "sofa", shape = "Sofa", x = 200.0, y = 100.0, width = 220.0, depth = 95.0, height = 85.0, rotation = 0, color = "#8A8580", form = "Round" } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, roundSofa.StatusCode);

        using var stranger = factory.CreateDefaultClient(factory.ClientOptions.BaseAddress, new CookieContainerHandler(new CookieContainer()));

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/plans/{id}")).StatusCode);
    }

    [Fact]
    public async Task A_rooms_finish_and_its_objects_materials_are_saved_and_read_back()
    {
        var finish = new
        {
            walls = "Plaster", accentWall = 0, accentColor = "#B5654A", accentFinish = "Brick", ceilingColor = "#3A3A3A", trimColor = "#1E1E1E",
            ceilingLight = "Chandelier", timeOfDay = "Evening",
        };
        var sofa = new { label = "sofa", shape = "Sofa", x = 200.0, y = 60.0, width = 200.0, depth = 90.0, height = 85.0, rotation = 0, color = "#8A8580", material = "velvet" };

        using var created = await Client.PostAsJsonAsync("/api/plans",
            new { name = "Sleep", roomType = "Bedroom", width = 4.0, length = 4.0, height = 2.6, wallColor = "#E8E2D6", floorColor = "#B89B7A" });
        var plan = (await created.Content.ReadFromJsonAsync<JsonElement>())!;
        var id = plan.GetProperty("id").GetGuid();

        // A furnished bedroom starts with the wall behind its bed in its own color, and its furniture in its materials.
        Assert.Equal((1, "Paint", "Pendant"), (plan.GetProperty("finish").GetProperty("accentWall").GetInt32(), plan.GetProperty("finish").GetProperty("walls").GetString(),
            plan.GetProperty("finish").GetProperty("ceilingLight").GetString()));
        Assert.Contains(plan.GetProperty("items").EnumerateArray(), i => i.GetProperty("material").GetString() == "linen upholstered");

        using var saved = await Client.PutAsJsonAsync($"/api/plans/{id}", new
        {
            name = "Sleep", width = 4.0, length = 4.0, height = 2.6, wallColor = "#E8E2D6", floorColor = "#B89B7A", view = 0, items = new[] { sofa }, finish,
        });
        using var kept = await Client.PutAsJsonAsync($"/api/plans/{id}", new
        {
            name = "Sleep", width = 4.0, length = 4.0, height = 2.6, wallColor = "#E8E2D6", floorColor = "#B89B7A", view = 0, items = new[] { sofa },
        });
        var read = await Client.GetFromJsonAsync<JsonElement>($"/api/plans/{id}");
        using var refused = await Client.PutAsJsonAsync($"/api/plans/{id}", new
        {
            name = "Sleep", width = 4.0, length = 4.0, height = 2.6, wallColor = "#E8E2D6", floorColor = "#B89B7A", view = 0, items = new[] { sofa },
            finish = new { finish.walls, accentWall = 9, finish.accentColor, finish.accentFinish, finish.ceilingColor, finish.trimColor, finish.ceilingLight, finish.timeOfDay },
        });
        var stored = read.GetProperty("finish");

        saved.EnsureSuccessStatusCode();
        kept.EnsureSuccessStatusCode();
        // Saving without a finish keeps the one the room has.
        Assert.Equal(("Plaster", 0, "Brick", "#3A3A3A", "Chandelier", "Evening"), (stored.GetProperty("walls").GetString(), stored.GetProperty("accentWall").GetInt32(),
            stored.GetProperty("accentFinish").GetString(), stored.GetProperty("ceilingColor").GetString(), stored.GetProperty("ceilingLight").GetString(),
            stored.GetProperty("timeOfDay").GetString()));
        Assert.Equal("velvet", read.GetProperty("items")[0].GetProperty("material").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
    }

    [Fact]
    public async Task An_uploaded_rooms_photo_is_read_into_a_tidied_plan_its_owner_can_build_on()
    {
        using var content = new ByteArrayContent(TestImages.Create(800, 600));
        using var notNow = new StringContent("false");
        using var stranger = factory.CreateDefaultClient(factory.ClientOptions.BaseAddress, new CookieContainerHandler(new CookieContainer()));

        content.Headers.ContentType = new MediaTypeHeaderValue(DesignApp.Application.Constants.ContentTypes.Jpeg);
        // As Build a room uploads it: only to read into a plan, so its analysis isn't started.
        using var form = new MultipartFormDataContent { { content, "photo", "room.jpg" }, { notNow, "analyze" } };
        using var uploaded = await Client.PostAsync("/api/rooms", form);
        var room = await uploaded.Content.ReadFromJsonAsync<JsonElement>();
        var roomId = room.GetProperty("id").GetGuid();

        using var built = await Client.PostAsync($"/api/rooms/{roomId}/plan", null);
        var answer = await built.Content.ReadFromJsonAsync<JsonElement>();
        var plan = answer.GetProperty("plan");
        var read = await Client.GetFromJsonAsync<JsonElement>($"/api/plans/{plan.GetProperty("id").GetGuid()}");
        using var refused = await stranger.PostAsync($"/api/rooms/{roomId}/plan", null);

        await factory.AgeRoomAsync(roomId, DateTimeOffset.UtcNow.AddDays(-2));

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DeleteUnsavedRooms>().HandleAsync(CancellationToken.None);
        }

        using var photo = await Client.GetAsync($"/api/rooms/{roomId}/photo");

        Assert.Equal("Waiting", room.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Created, built.StatusCode);
        // The fake AI's living room, linked to the photo, with every object kept.
        Assert.Equal(("LivingRoom", roomId), (read.GetProperty("roomType").GetString(), read.GetProperty("fromRoomId").GetGuid()));
        Assert.Equal(8, read.GetProperty("items").GetArrayLength());
        Assert.Equal(0, answer.GetProperty("leftOut").GetArrayLength());
        // Seen from where the photo was taken, kept with the plan.
        Assert.Equal(("Spot", 200.0, 470.0), (read.GetProperty("corner").GetString(), read.GetProperty("cameraSpot").GetProperty("x").GetDouble(),
            read.GetProperty("cameraSpot").GetProperty("y").GetDouble()));
        Assert.Contains(read.GetProperty("items").EnumerateArray(), i => i.GetProperty("label").GetString() == "three-seat sofa" && i.GetProperty("material").GetString() == "velvet");
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        // Not in a design, but a plan was read from it: the cleanup of unsaved uploads keeps it, so the builder can show it.
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
    }

    [Fact]
    public async Task A_described_object_and_a_generated_photo_come_from_the_ai()
    {
        using var created = await Client.PostAsJsonAsync("/api/plans",
            new { roomType = "Bedroom", width = 3.5, length = 4.0, height = 2.5, wallColor = "#F4F1EA", floorColor = "#C9A27E", furnish = false });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using var described = await Client.PostAsJsonAsync($"/api/plans/{id}/describe", new { description = "a black floor lamp" });

        described.EnsureSuccessStatusCode();
        var lamp = await described.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(("Lamp", "#2A2A2A"), (lamp.GetProperty("shape").GetString(), lamp.GetProperty("color").GetString()));
        using var blank = await Client.PostAsJsonAsync($"/api/plans/{id}/describe", new { description = " " });

        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);

        // A photo needs objects; with one, the fake AI's photo becomes a room that waits until the user starts its
        // analysis (with the real AI it costs money), and is then analyzed like an upload.
        Assert.Equal(HttpStatusCode.BadRequest, (await GenerateAsync(id)).StatusCode);
        using var saved = await Client.PutAsJsonAsync($"/api/plans/{id}", new
        {
            width = 3.5, length = 4.0, height = 2.5, wallColor = "#F4F1EA", floorColor = "#C9A27E",
            items = new[] { new { label = "bed", shape = "Bed", x = 175.0, y = 110.0, width = 160.0, depth = 210.0, height = 100.0, rotation = 0, color = "#E8DCC8" } },
        });

        saved.EnsureSuccessStatusCode();
        using var badStyle = await GenerateAsync(id, "Gothic");

        Assert.Equal(HttpStatusCode.BadRequest, badStyle.StatusCode);
        // A photo of another view: a wall the room has and a known spot; else 400. The plan keeps its own view.
        using var otherView = await GenerateAsync(id, "Boho", null, "2", "Right");

        Assert.Equal(HttpStatusCode.OK, otherView.StatusCode);
        using var badCorner = await GenerateAsync(id, "Boho", null, "2", "Ceiling");
        using var badWall = await GenerateAsync(id, "Boho", null, "9");

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest), (badCorner.StatusCode, badWall.StatusCode));
        Assert.Equal("None", (await Client.GetFromJsonAsync<JsonElement>($"/api/plans/{id}")).GetProperty("corner").GetString());
        using var generated = await GenerateAsync(id, "Boho", "warm evening light");

        Assert.Equal(HttpStatusCode.OK, generated.StatusCode);
        var room = await generated.Content.ReadFromJsonAsync<JsonElement>();
        var roomId = room.GetProperty("id").GetGuid();

        Assert.Equal("Waiting", room.GetProperty("status").GetString());
        await Task.Delay(500);
        Assert.Equal("Waiting", (await Client.GetFromJsonAsync<JsonElement>($"/api/rooms/{roomId}")).GetProperty("status").GetString());
        using var started = await Client.PostAsync($"/api/rooms/{roomId}/analysis", null);

        started.EnsureSuccessStatusCode();
        using var again = await Client.PostAsync($"/api/rooms/{roomId}/analysis", null);

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while ((await Client.GetFromJsonAsync<JsonElement>($"/api/rooms/{roomId}")).GetProperty("status").GetString() != "Ready")
        {
            Assert.True(DateTime.UtcNow < deadline, "The generated room wasn't analyzed in 30 seconds.");
            await Task.Delay(100);
        }
    }

    [Fact]
    public async Task A_runs_photos_are_one_room_to_change_choose_and_save_to_the_plans_design()
    {
        using var created = await Client.PostAsJsonAsync("/api/plans",
            new { roomType = "LivingRoom", width = 4.0, length = 5.0, height = 2.6, wallColor = "#E8E2D6", floorColor = "#B89B7A", furnish = true });
        var planId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // The run's first photo makes the room; the next goes into it.
        using var first = await GenerateAsync(planId, note: "Back wall, from the middle");
        var room = await first.Content.ReadFromJsonAsync<JsonElement>();
        var roomId = room.GetProperty("id").GetGuid();
        using var second = await GenerateAsync(planId, roomId: roomId, note: "Right wall");
        using var stranger = await GenerateAsync(planId, roomId: Guid.NewGuid());
        var takes = (await Client.GetFromJsonAsync<JsonElement[]>($"/api/rooms/{roomId}/takes"))!;

        Assert.Equal((HttpStatusCode.OK, roomId), (second.StatusCode, (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, stranger.StatusCode);
        Assert.Equal(["Back wall, from the middle", "Right wall"], takes.Select(t => t.GetProperty("note").GetString()));
        Assert.Equal(planId, room.GetProperty("planId").GetGuid());
        Assert.EndsWith($"?set={room.GetProperty("photoSetId").GetGuid()}", room.GetProperty("photoUrl").GetString(), StringComparison.Ordinal);

        // Each photo is served for good; a change adds one; choosing makes it the room's photo.
        using var photo = await Client.GetAsync(takes[1].GetProperty("photoUrl").GetString());

        Assert.Equal(("image/jpeg", true), (photo.Content.Headers.ContentType?.MediaType, photo.Headers.CacheControl?.ToString().Contains("immutable", StringComparison.Ordinal) == true));
        using var changed = await Client.PostAsJsonAsync($"/api/rooms/{roomId}/takes/{takes[1].GetProperty("id").GetGuid()}/change", new { instructions = "warm evening light" });
        using var blank = await Client.PostAsJsonAsync($"/api/rooms/{roomId}/takes/{takes[1].GetProperty("id").GetGuid()}/change", new { instructions = " " });

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.BadRequest), (changed.StatusCode, blank.StatusCode));
        Assert.Equal(3, (await Client.GetFromJsonAsync<JsonElement[]>($"/api/rooms/{roomId}/takes"))!.Length);
        using var chosen = await Client.PostAsync($"/api/rooms/{roomId}/takes/{takes[1].GetProperty("id").GetGuid()}/choose", null);
        var chosenRoom = await chosen.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(takes[1].GetProperty("id").GetGuid(), chosenRoom.GetProperty("chosenTakeId").GetGuid());
        // Choosing gives the room's files new addresses, so a browser never shows the photo it had before.
        Assert.NotEqual(room.GetProperty("photoSetId").GetGuid(), chosenRoom.GetProperty("photoSetId").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await Client.PostAsync($"/api/rooms/{roomId}/takes/{Guid.NewGuid()}/choose", null)).StatusCode);

        // A guest has no designs; once signed up, the room goes into the plan's own design.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsync($"/api/rooms/{roomId}/plan-design", null)).StatusCode);
        (await Client.PostAsJsonAsync("/api/account/sign-up", new { email = $"{Guid.NewGuid():N}@example.test", password = "correct horse battery" })).EnsureSuccessStatusCode();
        using var saved = await Client.PostAsync($"/api/rooms/{roomId}/plan-design", null);
        var designId = (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("designId").GetGuid();
        var design = await Client.GetFromJsonAsync<JsonElement>($"/api/designs/{designId}");

        Assert.Equal(designId, (await Client.GetFromJsonAsync<JsonElement>($"/api/plans/{planId}")).GetProperty("designId").GetGuid());
        Assert.Equal(roomId, design.GetProperty("rooms")[0].GetProperty("roomId").GetGuid());
        Assert.Contains("?set=", design.GetProperty("rooms")[0].GetProperty("photoUrl").GetString(), StringComparison.Ordinal);
    }

    /// <summary>Asks for a photo of a plan with a 3D view.</summary>
    /// <param name="id">The plan.</param>
    /// <param name="style">The photo's style, if any.</param>
    /// <param name="instructions">The user's extra instructions, if any.</param>
    /// <param name="view">The wall the photo looks at, when it isn't the room's own.</param>
    /// <param name="corner">Where the camera stands, with <paramref name="view"/>.</param>
    /// <param name="roomId">The room of the run's earlier photos, if any.</param>
    /// <param name="note">What the photo is, if anything.</param>
    private async Task<HttpResponseMessage> GenerateAsync(
        Guid id, string? style = null, string? instructions = null, string? view = null, string? corner = null, Guid? roomId = null, string? note = null)
    {
        using var guide = new ByteArrayContent(TestImages.Create(1280, 960));

        guide.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        using var form = new MultipartFormDataContent { { guide, "guide", "guide.png" } };

        if (style is not null)
        {
            form.Add(new StringContent(style), "style");
        }

        if (instructions is not null)
        {
            form.Add(new StringContent(instructions), "instructions");
        }

        if (view is not null)
        {
            form.Add(new StringContent(view), "view");
        }

        if (corner is not null)
        {
            form.Add(new StringContent(corner), "corner");
        }

        if (roomId is not null)
        {
            form.Add(new StringContent(roomId.Value.ToString()), "roomId");
        }

        if (note is not null)
        {
            form.Add(new StringContent(note), "note");
        }

        return await Client.PostAsync($"/api/plans/{id}/generate", form);
    }
}
