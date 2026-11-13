using DesignApp.Api.Helpers;
using DesignApp.Api.Requests;
using DesignApp.Api.Responses;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Application.UseCases;
using DesignApp.Domain.Entities;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using static DesignApp.Domain.Constants;

namespace DesignApp.Api.Endpoints;

/// <summary>The <c>/api/rooms</c> endpoints: upload a room photo, read a room, its photo and label map, and fix outlines.</summary>
internal static class RoomEndpoints
{
    /// <summary>Maps the room endpoints under <c>/rooms</c>.</summary>
    /// <param name="api">The <c>/api</c> route group.</param>
    /// <returns>The same group, for chaining.</returns>
    public static RouteGroupBuilder MapRoomEndpoints(this RouteGroupBuilder api)
    {
        var rooms = api.MapGroup(Constants.Routes.Rooms).WithTags(Constants.OpenApi.RoomsTag);

        rooms.MapPost(Constants.Routes.Root, UploadAsync)
            .WithName("UploadRoom")
            .WithSummary("Uploads a room photo and creates a room for it.")
            .WithDescription($"Send the photo as multipart form data in a field named `photo`. JPEG, PNG or WebP up to {Application.Constants.Photos.MaxUploadMegabytes} MB. " +
                "An optional `scope` field chooses what the AI looks for: `WholeRoom` (default), `NoDoorsOrWindows` or `ObjectsOnly`. " +
                "An optional `ai` field chooses `Fake` or `Real` where `GET /api/ai` allows it.")
            .DisableAntiforgery() // the cookies are SameSite=Lax, so another site's form posts arrive without them
            .RequireRateLimiting(Constants.RateLimits.UploadsPolicy);

        rooms.MapGet(Constants.Routes.ById, GetAsync)
            .WithName("GetRoom")
            .WithSummary("Returns a room with its analysis status and detected objects.");

        rooms.MapGet(Constants.Routes.RoomPhoto, GetPhotoAsync)
            .WithName("GetRoomPhoto")
            .WithSummary($"Returns the room's photo as stored: upright, at most {Application.Constants.Photos.MaxLongEdge} px, without metadata.");

        rooms.MapGet(Constants.Routes.RoomPhotoVersion, GetPhotoVersionAsync)
            .WithName("GetRoomPhotoVersion")
            .WithSummary("Returns one stored version of the room's photo: 1 and up are made by removing objects (0 is `photo`).")
            .WithDescription("Each version never changes, so it can be cached for good.");

        rooms.MapGet(Constants.Routes.RoomLabels, GetLabelMapAsync)
            .WithName("GetRoomLabelMap")
            .WithSummary("Returns the room's label map: a grayscale PNG where each pixel's value is the index of the object it belongs to (0 = none).")
            .WithDescription($"Available once the room is Ready. It is the size of the analysis image (at most {Application.Constants.Analysis.MaxLongEdge} px), not the photo; scale it to fit.");

        rooms.MapGet(Constants.Routes.RoomLabelRevision, GetLabelMapRevisionAsync)
            .WithName("GetRoomLabelMapRevision")
            .WithSummary("Returns one stored version of the room's label map: 0 from the analysis, then one per outline fix.")
            .WithDescription("Each version never changes, so it can be cached for good.");

        rooms.MapPost(Constants.Routes.RoomOutline, FixOutlineAsync)
            .WithName("FixRoomOutline")
            .WithSummary("Paints or erases one object's outline with a brush stroke: a round brush of `radius` label-map pixels stamped along `points`.")
            .WithDescription("Starts from `baseRevision`, the label map version the editor shows, and stores the result as a new version that becomes current.");

        rooms.MapPost(Constants.Routes.RoomObjects, AddObjectAsync)
            .WithName("AddRoomObject")
            .WithSummary("Adds an object the user marked by hand: `strokes` (Paint marks, Erase unmarks, in order), each a round brush of `radius` label-map pixels stamped along `points`.")
            .WithDescription("Starts from `baseRevision`; the marked pixels get the new object's index on top of whatever was there, and the result is stored as a new label map version that becomes current. The box and main color are measured from the photo.");

        rooms.MapPost(Constants.Routes.RoomRemovals, RemoveObjectsAsync)
            .WithName("RemoveRoomObjects")
            .WithSummary("Removes objects from the photo together (`objectIndexes`): the room's AI fills their pixels with what is behind them in one call.")
            .WithDescription("Starts from `baseRevision`; the new photo and a label map where the object's pixels belong to the objects around it are stored as a new version that becomes current (photo version in `photoVersions`). With the real AI each removal is a paid call, so it is rate-limited.")
            .RequireRateLimiting(Constants.RateLimits.RemovalsPolicy);

        rooms.MapPost(Constants.Routes.RoomDepth, MeasureDepthAsync)
            .WithName("MeasureRoomDepth")
            .WithSummary("Gives the room its depth map for the 3D view, measuring it once from the original photo if it has none.")
            .WithDescription("With the real AI the first call is a paid request, so it is rate-limited; later calls return the stored map. Sample rooms bring theirs.")
            .RequireRateLimiting(Constants.RateLimits.RemovalsPolicy);

        rooms.MapGet(Constants.Routes.RoomDepth, GetDepthAsync)
            .WithName("GetRoomDepth")
            .WithSummary("Returns the room's depth map: a PNG grid, 16 bits per cell in red (high) and green (low), relative to the farthest cell.")
            .WithDescription("Available once measured; it never changes, so it can be cached for good.");

        rooms.MapGet(Constants.Routes.RoomBackdrop, GetBackdropAsync)
            .WithName("GetRoomBackdrop")
            .WithSummary("Returns one file of the room's empty-room backdrop for the 3D view: `photo`, `labels` or `depth`.")
            .WithDescription("Only rooms that have one (sample rooms prepared ahead of time); the files never change, so they can be cached for good.");

        rooms.MapPut(Constants.Routes.RoomCurrentLabels, UseLabelMapRevisionAsync)
            .WithName("UseRoomLabelMapRevision")
            .WithSummary("Makes a stored label map version current again (undo or redo of an outline fix).");

        api.MapGet(Constants.Routes.Ai, (IAiProviders aiProviders) => TypedResults.Ok(AiAvailabilityResponse.From(aiProviders.Availability)))
            .WithTags(Constants.OpenApi.RoomsTag)
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
        if (AiModeChoice.TryResolve(ai, aiProviders.Availability, out var aiMode) is { } problem)
            return problem;

        var analysisScope = AnalysisScope.WholeRoom;
        if (!string.IsNullOrWhiteSpace(scope) && (!Enum.TryParse(scope, ignoreCase: true, out analysisScope) || !Enum.IsDefined(analysisScope)))
        {
            return TypedResults.Problem(
                Constants.Messages.UnknownScope(scope, Enum.GetNames<AnalysisScope>()),
                statusCode: StatusCodes.Status400BadRequest,
                title: Constants.Titles.InvalidScope);
        }

        if (photo.Length > Application.Constants.Photos.MaxUploadBytes)
        {
            return TypedResults.Problem(
                Constants.Messages.PhotoTooLarge,
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: Constants.Titles.PhotoTooLarge);
        }

        await using var content = photo.OpenReadStream();
        var result = await uploadRoom.HandleAsync(content, cancellationToken, analysisScope, aiMode);
        if (!result.Succeeded)
            return TypedResults.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest, title: Constants.Titles.InvalidPhoto);

        var room = result.Value;
        return TypedResults.Created(Constants.Routes.RoomUrl(room.Id), RoomResponse.From(room));
    }

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

    /// <summary><c>GET /api/rooms/{id}/photo/{version}</c>: streams one stored version of the photo.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="version">The photo version (1 and up).</param>
    /// <param name="getRoom">The use case that loads the room.</param>
    /// <param name="storage">Where the photos are stored.</param>
    /// <param name="response">Used to set the caching headers.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the JPEG, or 404 when the room or that version is missing.</returns>
    private static async Task<Results<FileStreamHttpResult, NotFound>> GetPhotoVersionAsync(
        Guid id, int version, GetRoom getRoom, IFileStorage storage, HttpResponse response, CancellationToken cancellationToken)
    {
        var room = await getRoom.HandleAsync(id, cancellationToken);
        return room is not null && version >= 0 && version <= room.LatestPhotoVersion
            ? await StreamImmutableAsync(Application.Constants.Rooms.PhotoKey(room.Id, version), storage, response, cancellationToken)
            : TypedResults.NotFound();
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
            ? await StreamImmutableAsync(Application.Constants.Rooms.LabelMapKey(room.Id, revision), storage, response, cancellationToken)
            : TypedResults.NotFound();
    }

    /// <summary><c>POST /api/rooms/{id}/outline</c>: paints or erases one object's outline with a brush, box or lasso stroke.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="request">The stroke.</param>
    /// <param name="fixOutline">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the room and its new label map version, 400 when the fix can't be made, or 404.</returns>
    private static async Task<Results<Ok<RoomResponse>, ProblemHttpResult, NotFound>> FixOutlineAsync(
        Guid id, OutlineStrokeRequest request, FixRoomOutline fixOutline, CancellationToken cancellationToken)
    {
        var outcome = await fixOutline.FixAsync(
            id, request.BaseRevision, request.ObjectIndex, request.Mode, request.Radius, request.Points ?? [], request.Shape, cancellationToken);
        return ToResponse(outcome, Constants.Titles.OutlineFixFailed);
    }

    /// <summary><c>POST /api/rooms/{id}/objects</c>: adds an object the user marked by hand.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="request">The name, type and strokes.</param>
    /// <param name="addObject">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the room and its new label map version, 400 when the object can't be added, or 404.</returns>
    private static async Task<Results<Ok<RoomResponse>, ProblemHttpResult, NotFound>> AddObjectAsync(
        Guid id, NewObjectRequest request, AddRoomObject addObject, CancellationToken cancellationToken)
    {
        var outcome = await addObject.AddAsync(
            id, request.BaseRevision, request.Label, request.Category, request.Strokes ?? [], cancellationToken);
        return ToResponse(outcome, Constants.Titles.NewObjectFailed);
    }

    /// <summary><c>POST /api/rooms/{id}/removals</c>: removes objects from the photo together.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="request">The label map version the editor shows and the objects to remove.</param>
    /// <param name="removeObject">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the room and its new version, 400 when the objects can't be removed, or 404.</returns>
    private static async Task<Results<Ok<RoomResponse>, ProblemHttpResult, NotFound>> RemoveObjectsAsync(
        Guid id, RemoveObjectsRequest request, RemoveRoomObject removeObject, CancellationToken cancellationToken)
    {
        var outcome = await removeObject.RemoveAsync(id, request.BaseRevision, request.ObjectIndexes ?? [], cancellationToken);
        return ToResponse(outcome, Constants.Titles.RemovalFailed);
    }

    /// <summary><c>POST /api/rooms/{id}/depth</c>: gives the room its depth map, measuring it if needed.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="measureDepth">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the room and its depth map, 400 when it can't be measured, or 404.</returns>
    private static async Task<Results<Ok<RoomResponse>, ProblemHttpResult, NotFound>> MeasureDepthAsync(
        Guid id, MeasureRoomDepth measureDepth, CancellationToken cancellationToken)
    {
        var outcome = await measureDepth.HandleAsync(id, cancellationToken);
        return ToResponse(outcome, Constants.Titles.DepthFailed);
    }

    /// <summary><c>GET /api/rooms/{id}/depth</c>: streams the room's depth map.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="getRoom">The use case that loads the room.</param>
    /// <param name="storage">Where the depth map is stored.</param>
    /// <param name="response">Used to set the caching headers.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the PNG, or 404 when the room is missing or has no depth map yet.</returns>
    private static async Task<Results<FileStreamHttpResult, NotFound>> GetDepthAsync(
        Guid id, GetRoom getRoom, IFileStorage storage, HttpResponse response, CancellationToken cancellationToken)
    {
        var room = await getRoom.HandleAsync(id, cancellationToken);
        return room?.DepthKey is { } key ? await StreamImmutableAsync(key, storage, response, cancellationToken) : TypedResults.NotFound();
    }

    /// <summary><c>GET /api/rooms/{id}/backdrop/{file}</c>: streams one file of the room's empty-room backdrop.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="file">photo, labels or depth.</param>
    /// <param name="getRoom">The use case that loads the room.</param>
    /// <param name="storage">Where the files are stored.</param>
    /// <param name="response">Used to set the caching headers.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the file, or 404 when the room is missing, has no backdrop, or the file name is unknown.</returns>
    private static async Task<Results<FileStreamHttpResult, NotFound>> GetBackdropAsync(
        Guid id, string file, GetRoom getRoom, IFileStorage storage, HttpResponse response, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Application.Constants.BackdropFile>(file, ignoreCase: true, out var which) || !Enum.IsDefined(which))
            return TypedResults.NotFound();
        var room = await getRoom.HandleAsync(id, cancellationToken);
        return room is { HasBackdrop: true }
            ? await StreamImmutableAsync(Application.Constants.Rooms.BackdropKey(room.Id, which), storage, response, cancellationToken)
            : TypedResults.NotFound();
    }

    /// <summary><c>PUT /api/rooms/{id}/labels/current</c>: makes a stored label map version current.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="request">The version.</param>
    /// <param name="fixOutline">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the room, 400 for an unknown version, or 404.</returns>
    private static async Task<Results<Ok<RoomResponse>, ProblemHttpResult, NotFound>> UseLabelMapRevisionAsync(
        Guid id, LabelMapRevisionRequest request, FixRoomOutline fixOutline, CancellationToken cancellationToken)
    {
        var outcome = await fixOutline.UseRevisionAsync(id, request.Revision, cancellationToken);
        return ToResponse(outcome, Constants.Titles.UnknownOutlineVersion);
    }

    /// <summary>Turns a use case's outcome into a response.</summary>
    /// <param name="result">The outcome; <c>null</c> when the room wasn't found.</param>
    /// <param name="title">The problem title for a refusal.</param>
    private static Results<Ok<RoomResponse>, ProblemHttpResult, NotFound> ToResponse(Result<Room>? result, string title)
    {
        if (result is null)
            return TypedResults.NotFound();

        return result.Succeeded
            ? TypedResults.Ok(RoomResponse.From(result.Value))
            : TypedResults.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest, title: title);
    }

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
        response.Headers.CacheControl = Constants.Http.ImmutableCache;
        response.Headers.Vary = Constants.Http.Origin;
        return TypedResults.Stream(file.Content, file.ContentType);
    }
}
