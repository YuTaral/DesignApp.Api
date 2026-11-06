using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DesignApp.Tests;
using SkiaSharp;

namespace DesignApp.IntegrationTests;

/// <summary>End-to-end tests of the <c>/api/rooms</c> endpoints over HTTP.</summary>
/// <param name="factory">The API running against test containers.</param>
public class RoomsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Uploading_a_photo_creates_a_room()
    {
        using var response = await UploadAsync(TestImages.Create(1200, 800));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var room = await response.Content.ReadFromJsonAsync<RoomDto>();
        Assert.NotNull(room);
        Assert.Equal("Uploaded", room.Status);
        Assert.Equal((1200, 800), (room.Width, room.Height));
        Assert.Equal(DesignApp.Api.Constants.Routes.RoomPhotoUrl(room.Id), room.PhotoUrl);
        Assert.Empty(room.Objects);
        Assert.Equal($"/api/rooms/{room.Id}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task An_uploaded_room_and_its_photo_can_be_read_back()
    {
        using var upload = await UploadAsync(TestImages.Create(6000, 3000, SKEncodedImageFormat.Png), "big.png", DesignApp.Application.Constants.ContentTypes.Png);
        var created = await upload.Content.ReadFromJsonAsync<RoomDto>();

        var room = await _client.GetFromJsonAsync<RoomDto>($"/api/rooms/{created!.Id}");
        Assert.Equal(created.Id, room!.Id);
        Assert.Equal((4096, 2048), (room.Width, room.Height));

        using var photo = await _client.GetAsync(room.PhotoUrl);
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal(DesignApp.Application.Constants.ContentTypes.Jpeg, photo.Content.Headers.ContentType?.MediaType);
        Assert.True(photo.Headers.CacheControl?.Private);
        Assert.Contains("Origin", photo.Headers.Vary);
        using var decoded = TestImages.Decode(await photo.Content.ReadAsByteArrayAsync());
        Assert.Equal((4096, 2048), (decoded.Width, decoded.Height));
    }

    [Fact]
    public async Task Uploading_something_that_is_not_a_photo_returns_a_readable_error()
    {
        using var response = await UploadAsync("hello"u8.ToArray(), "notes.jpg", DesignApp.Application.Constants.ContentTypes.Jpeg);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(DesignApp.Api.Constants.Titles.InvalidPhoto, problem.GetProperty("title").GetString());
        Assert.Equal(DesignApp.Infrastructure.Constants.Messages.UnsupportedPhoto, problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Uploading_without_a_file_returns_400()
    {
        using var form = new MultipartFormDataContent { { new StringContent("x"), "not-a-photo" } };

        using var response = await _client.PostAsync("/api/rooms", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Uploading_a_file_over_the_limit_returns_413()
    {
        using var response = await UploadAsync(new byte[15 * 1024 * 1024 + 1]);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_room_returns_404()
    {
        using var room = await _client.GetAsync($"/api/rooms/{Guid.NewGuid()}");
        using var photo = await _client.GetAsync($"/api/rooms/{Guid.NewGuid()}/photo");

        Assert.Equal(HttpStatusCode.NotFound, room.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, photo.StatusCode);
    }

    /// <summary>Posts a file to <c>/api/rooms</c> the way the browser does.</summary>
    /// <param name="file">The file's bytes.</param>
    /// <param name="fileName">The file name sent in the form.</param>
    /// <param name="contentType">The MIME type sent in the form.</param>
    private async Task<HttpResponseMessage> UploadAsync(byte[] file, string fileName = "room.jpg", string contentType = DesignApp.Application.Constants.ContentTypes.Jpeg)
    {
        using var content = new ByteArrayContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var form = new MultipartFormDataContent { { content, "photo", fileName } };
        return await _client.PostAsync("/api/rooms", form);
    }

    /// <summary>The room JSON as a client reads it, kept separate from the API's own type.</summary>
    private sealed record RoomDto(Guid Id, string Status, int Width, int Height, string PhotoUrl, DateTimeOffset CreatedAt, string? FailureReason, List<JsonElement> Objects);
}
