using DesignApp.Application.Abstractions;
using DesignApp.Application.Analysis;
using DesignApp.Application.Common;
using DesignApp.Application.Photos;
using DesignApp.Application.Rooms;
using DesignApp.Domain.Rooms;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace DesignApp.Api.Rooms;

/// <summary>The <c>/api/rooms</c> endpoints: upload a room photo, read a room, its photo and label map, and fix outlines.</summary>
internal static class RoomEndpoints
{
    /// <summary>Maps the room endpoints under <c>/rooms</c>.</summary>
    /// <param name="api">The <c>/api</c> route group.</param>
    /// <returns>The same group, for chaining.</returns>
    public static RouteGroupBuilder MapRoomEndpoints(this RouteGroupBuilder api)
    {
        var rooms = api.MapGroup("/rooms").WithTags("Rooms");

        rooms.MapPost("/", UploadAsync)
            .WithName("UploadRoom")
            .WithSummary("Uploads a room photo and creates a room for it.")
            .WithDescription("Send the photo as multipart form data in a field named `photo`. JPEG, PNG or WebP up to 15 MB. " +
                "An optional `scope` field chooses what the AI looks for: `WholeRoom` (default), `NoDoorsOrWindows` or `ObjectsOnly`. " +
                "An optional `ai` field chooses `Fake` or `Real` where `GET /api/ai` allows it.")
            .DisableAntiforgery() // the cookies are SameSite=Lax, so another site's form posts arrive without them
            .RequireRateLimiting(RateLimitPolicies.Uploads);

        rooms.MapGet("/", ListAsync)
            .WithName("ListRooms")
            .WithSummary("Lists the current user's rooms, newest first: the account's, or this browser's while signed out.");

        rooms.MapGet("/{id:guid}", GetAsync)
            .WithName("GetRoom")
            .WithSummary("Returns a room with its analysis status and detected objects.");

        rooms.MapGet("/{id:guid}/photo", GetPhotoAsync)
            .WithName("GetRoomPhoto")
            .WithSummary("Returns the room's photo as stored: upright, at most 4096 px, without metadata.");

        rooms.MapGet("/{id:guid}/labels", GetLabelMapAsync)
            .WithName("GetRoomLabelMap")
            .WithSummary("Returns the room's label map: a grayscale PNG where each pixel's value is the index of the object it belongs to (0 = none).")
            .WithDescription("Available once the room is Ready. It is the size of the analysis image (at most 2048 px), not the photo; scale it to fit.");

        rooms.MapGet("/{id:guid}/labels/{revision:int}", GetLabelMapRevisionAsync)
            .WithName("GetRoomLabelMapRevision")
            .WithSummary("Returns one stored version of the room's label map: 0 from the analysis, then one per outline fix.")
            .WithDescription("Each version never changes, so it can be cached for good.");

        rooms.MapPost("/{id:guid}/outline", FixOutlineAsync)
            .WithName("FixRoomOutline")
            .WithSummary("Paints or erases one object's outline with a brush stroke: a round brush of `radius` label-map pixels stamped along `points`.")
            .WithDescription("Starts from `baseRevision`, the label map version the editor shows, and stores the result as a new version that becomes current.");

        rooms.MapPut("/{id:guid}/labels/current", UseLabelMapRevisionAsync)
            .WithName("UseRoomLabelMapRevision")
            .WithSummary("Makes a stored label map version current again (undo or redo of an outline fix).");

        api.MapGet("/ai", (IAiProviders aiProviders) => TypedResults.Ok(AiAvailabilityResponse.From(aiProviders.Availability)))
            .WithTags("Rooms")
            .WithName("GetAiAvailability")
            .WithSummary("Says which AI the uploads use, and whether each upload may choose between the fake and the real AI.");

        return api;
    }

    /// <summary><c>POST /api/rooms</c>: validates and stores the photo, then creates the room.</summary>
    /// <param name="photo">The uploaded file from the <c>photo</c> form field.</param>
    /// <param name="scope">The optional <c>scope</c> form field: what the AI looks for.</param>
    /// <param name="ai">The optional <c>ai</c> form field: Fake or Real.</param>
    /// <param name="aiProviders">Says which AI modes may be used.</param>
    /// <param name="uploadRoom">The use case that processes and stores the photo.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>201 with the new room, 400 for an unusable photo, or 413 when it is too large.</returns>
    private static async Task<Results<Created<RoomResponse>, ProblemHttpResult>> UploadAsync(
        IFormFile photo, [FromForm] string? scope, [FromForm] string? ai, UploadRoom uploadRoom, IAiProviders aiProviders,
        CancellationToken cancellationToken)
    {
        var availability = aiProviders.Availability;
        var aiMode = availability.Default;
        if (!string.IsNullOrWhiteSpace(ai) && (!Enum.TryParse(ai, ignoreCase: true, out aiMode) || !Enum.IsDefined(aiMode)))
            return TypedResults.Problem($"Unknown AI \"{ai}\". Use Fake or Real.", statusCode: StatusCodes.Status400BadRequest, title: "Invalid AI");
        if (!availability.Allows(aiMode))
        {
            var why = aiMode is AiMode.Real && !availability.RealAvailable
                ? "The real AI isn't set up yet: add the Anthropic and fal.ai keys to the API's user secrets."
                : $"This server only uses the {availability.Default.ToString().ToLowerInvariant()} AI.";
            return TypedResults.Problem(why, statusCode: StatusCodes.Status400BadRequest, title: "AI not available");
        }

        var analysisScope = AnalysisScope.WholeRoom;
        if (!string.IsNullOrWhiteSpace(scope) && (!Enum.TryParse(scope, ignoreCase: true, out analysisScope) || !Enum.IsDefined(analysisScope)))
        {
            return TypedResults.Problem(
                $"Unknown scope \"{scope}\". Use {string.Join(", ", Enum.GetNames<AnalysisScope>())}.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid scope");
        }

        if (photo.Length > PhotoRules.MaxUploadBytes)
        {
            return TypedResults.Problem(
                $"Photos can be up to {PhotoRules.MaxUploadBytes / (1024 * 1024)} MB.",
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Photo too large");
        }

        await using var content = photo.OpenReadStream();
        var result = await uploadRoom.HandleAsync(content, cancellationToken, analysisScope, aiMode);
        if (!result.Succeeded)
            return TypedResults.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest, title: "Invalid photo");

        var room = result.Value;
        return TypedResults.Created($"/api/rooms/{room.Id}", RoomResponse.From(room));
    }

    /// <summary><c>GET /api/rooms</c>: the current user's rooms.</summary>
    /// <param name="getRoom">The use case that loads the rooms.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    private static async Task<Ok<List<RoomResponse>>> ListAsync(GetRoom getRoom, CancellationToken cancellationToken) =>
        TypedResults.Ok((await getRoom.AllAsync(cancellationToken)).Select(RoomResponse.From).ToList());

    /// <summary><c>GET /api/rooms/{id}</c>: returns one of the current user's rooms.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="getRoom">The use case that loads the room.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the room, or 404 when the user has no room with that id.</returns>
    private static async Task<Results<Ok<RoomResponse>, NotFound>> GetAsync(
        Guid id, GetRoom getRoom, CancellationToken cancellationToken)
    {
        var room = await getRoom.HandleAsync(id, cancellationToken);
        return room is null ? TypedResults.NotFound() : TypedResults.Ok(RoomResponse.From(room));
    }

    /// <summary><c>GET /api/rooms/{id}/photo</c>: streams the room's stored photo.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="getRoom">The use case that loads the room.</param>
    /// <param name="storage">Where the photo file is stored.</param>
    /// <param name="response">Used to set the caching headers.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the JPEG, or 404 when the room or its file is missing.</returns>
    private static async Task<Results<FileStreamHttpResult, NotFound>> GetPhotoAsync(
        Guid id, GetRoom getRoom, IFileStorage storage, HttpResponse response, CancellationToken cancellationToken)
    {
        var room = await getRoom.HandleAsync(id, cancellationToken);
        return room is null ? TypedResults.NotFound() : await StreamImmutableAsync(room.PhotoKey, storage, response, cancellationToken);
    }

    /// <summary><c>GET /api/rooms/{id}/labels</c>: streams the room's label map.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="getRoom">The use case that loads the room.</param>
    /// <param name="storage">Where the label map is stored.</param>
    /// <param name="response">Used to set the caching headers.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the PNG, or 404 when the room is missing or not analyzed yet.</returns>
    private static async Task<Results<FileStreamHttpResult, NotFound>> GetLabelMapAsync(
        Guid id, GetRoom getRoom, IFileStorage storage, HttpResponse response, CancellationToken cancellationToken)
    {
        var room = await getRoom.HandleAsync(id, cancellationToken);
        return room?.LabelMapKey is { } key ? await StreamImmutableAsync(key, storage, response, cancellationToken) : TypedResults.NotFound();
    }

    /// <summary><c>GET /api/rooms/{id}/labels/{revision}</c>: streams one stored version of the label map.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="revision">The version.</param>
    /// <param name="getRoom">The use case that loads the room.</param>
    /// <param name="storage">Where the label maps are stored.</param>
    /// <param name="response">Used to set the caching headers.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the PNG, or 404 when the room or that version is missing.</returns>
    private static async Task<Results<FileStreamHttpResult, NotFound>> GetLabelMapRevisionAsync(
        Guid id, int revision, GetRoom getRoom, IFileStorage storage, HttpResponse response, CancellationToken cancellationToken)
    {
        var room = await getRoom.HandleAsync(id, cancellationToken);
        return room is not null && room.HasLabelMapRevision(revision)
            ? await StreamImmutableAsync(StorageKeys.RoomLabelMap(room.Id, revision), storage, response, cancellationToken)
            : TypedResults.NotFound();
    }

    /// <summary><c>POST /api/rooms/{id}/outline</c>: paints or erases one object's outline with a brush stroke.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="request">The stroke.</param>
    /// <param name="fixOutline">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the room and its new label map version, 400 when the fix can't be made, or 404.</returns>
    private static async Task<Results<Ok<RoomResponse>, ProblemHttpResult, NotFound>> FixOutlineAsync(
        Guid id, OutlineStrokeRequest request, FixRoomOutline fixOutline, CancellationToken cancellationToken) =>
        ToResponse(await fixOutline.FixAsync(id, request.BaseRevision, request.ObjectIndex, request.Mode, request.Radius,
            request.Points ?? [], cancellationToken), "Can't fix the outline");

    /// <summary><c>PUT /api/rooms/{id}/labels/current</c>: makes a stored label map version current.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="request">The version.</param>
    /// <param name="fixOutline">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the room, 400 for an unknown version, or 404.</returns>
    private static async Task<Results<Ok<RoomResponse>, ProblemHttpResult, NotFound>> UseLabelMapRevisionAsync(
        Guid id, LabelMapRevisionRequest request, FixRoomOutline fixOutline, CancellationToken cancellationToken) =>
        ToResponse(await fixOutline.UseRevisionAsync(id, request.Revision, cancellationToken), "Unknown outline version");

    /// <summary>Turns a use case's outcome into a response.</summary>
    /// <param name="result">The outcome; <c>null</c> when the room wasn't found.</param>
    /// <param name="title">The problem title for a refusal.</param>
    private static Results<Ok<RoomResponse>, ProblemHttpResult, NotFound> ToResponse(Result<Room>? result, string title) =>
        result is null ? TypedResults.NotFound()
        : result.Succeeded ? TypedResults.Ok(RoomResponse.From(result.Value))
        : TypedResults.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest, title: title);

    /// <summary>Streams a stored file that never changes once written.</summary>
    /// <param name="key">The file's storage key.</param>
    /// <param name="storage">Where the file is stored.</param>
    /// <param name="response">Used to set the caching headers.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the file, or 404 when it is missing.</returns>
    private static async Task<Results<FileStreamHttpResult, NotFound>> StreamImmutableAsync(
        string key, IFileStorage storage, HttpResponse response, CancellationToken cancellationToken)
    {
        var file = await storage.OpenReadAsync(key, cancellationToken);
        if (file is null)
            return TypedResults.NotFound();

        // These files never change, so browsers can keep them for good. Vary on Origin so a copy
        // cached by a plain <img> is never reused for a CORS request (the WebGL editor), which would fail.
        response.Headers.CacheControl = "private, max-age=31536000, immutable";
        response.Headers.Vary = "Origin";
        return TypedResults.Stream(file.Content, file.ContentType);
    }
}
