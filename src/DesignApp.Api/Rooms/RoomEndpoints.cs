using DesignApp.Application.Abstractions;
using DesignApp.Application.Photos;
using DesignApp.Application.Rooms;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DesignApp.Api.Rooms;

internal static class RoomEndpoints
{
    public static RouteGroupBuilder MapRoomEndpoints(this RouteGroupBuilder api)
    {
        var rooms = api.MapGroup("/rooms").WithTags("Rooms");

        rooms.MapPost("/", UploadAsync)
            .WithName("UploadRoom")
            .WithSummary("Uploads a room photo and creates a room for it.")
            .WithDescription("Send the photo as multipart form data in a field named `photo`. JPEG, PNG or WebP up to 15 MB.")
            .DisableAntiforgery() // no cookie auth yet; revisit with sign-in (milestone 5)
            .RequireRateLimiting(RateLimitPolicies.Uploads);

        rooms.MapGet("/{id:guid}", GetAsync)
            .WithName("GetRoom")
            .WithSummary("Returns a room with its analysis status and detected objects.");

        rooms.MapGet("/{id:guid}/photo", GetPhotoAsync)
            .WithName("GetRoomPhoto")
            .WithSummary("Returns the room's photo as stored: upright, at most 4096 px, without metadata.");

        return api;
    }

    private static async Task<Results<Created<RoomResponse>, ProblemHttpResult>> UploadAsync(
        IFormFile photo, UploadRoom uploadRoom, CancellationToken cancellationToken)
    {
        if (photo.Length > PhotoRules.MaxUploadBytes)
        {
            return TypedResults.Problem(
                $"Photos can be up to {PhotoRules.MaxUploadBytes / (1024 * 1024)} MB.",
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Photo too large");
        }

        await using var content = photo.OpenReadStream();
        var room = await uploadRoom.HandleAsync(content, cancellationToken);
        return TypedResults.Created($"/api/rooms/{room.Id}", RoomResponse.From(room));
    }

    private static async Task<Results<Ok<RoomResponse>, NotFound>> GetAsync(
        Guid id, GetRoom getRoom, CancellationToken cancellationToken)
    {
        var room = await getRoom.HandleAsync(id, cancellationToken);
        return room is null ? TypedResults.NotFound() : TypedResults.Ok(RoomResponse.From(room));
    }

    private static async Task<Results<FileStreamHttpResult, NotFound>> GetPhotoAsync(
        Guid id, GetRoom getRoom, IFileStorage storage, HttpResponse response, CancellationToken cancellationToken)
    {
        var room = await getRoom.HandleAsync(id, cancellationToken);
        if (room is null)
            return TypedResults.NotFound();

        var file = await storage.OpenReadAsync(room.PhotoKey, cancellationToken);
        if (file is null)
            return TypedResults.NotFound();

        // A room's photo never changes, so browsers can keep it for good. Vary on Origin so a copy
        // cached by a plain <img> is never reused for a CORS request (the WebGL editor), which would fail.
        response.Headers.CacheControl = "private, max-age=31536000, immutable";
        response.Headers.Vary = "Origin";
        return TypedResults.Stream(file.Content, file.ContentType);
    }
}
