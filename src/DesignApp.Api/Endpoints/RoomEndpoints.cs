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
                "An optional `ai` field chooses `Fake` or `Real` where `GET /api/ai` allows it. " +
                "An optional `analyze` field set to `false` leaves the room waiting instead of starting its analysis (which the real AI charges for), " +
                "such as for a photo only read into a room plan.")
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

        rooms.MapGet(Constants.Routes.RoomTakes, GetTakesAsync)
            .WithName("GetRoomTakes")
            .WithSummary("Lists the room's photos to choose from (a room builder run's photos and their AI changes), oldest first.");

        rooms.MapGet(Constants.Routes.RoomTakePhoto, GetTakePhotoAsync)
            .WithName("GetRoomTakePhoto")
            .WithSummary("Returns one of the room's photos to choose from.")
            .WithDescription("Each photo never changes, so it can be cached for good.");

        rooms.MapPost(Constants.Routes.RoomTakeChange, ChangeTakeAsync)
            .WithName("ChangeRoomTake")
            .WithSummary("Has the room's AI make a new version of one of its photos with a change in the user's words (`instructions`).")
            .WithDescription("The new photo is added to the room's photos; the room's own photo stays as it is. With the real AI this is a paid call, so it is rate-limited.")
            .RequireRateLimiting(Constants.RateLimits.RemovalsPolicy);

        rooms.MapPost(Constants.Routes.RoomTakeChoice, ChooseTakeAsync)
            .WithName("ChooseRoomTake")
            .WithSummary("Makes one of the room's photos the room's photo, the one its analysis uses.")
            .WithDescription("A room already analyzed with another photo waits for its analysis again: its outlines, removals and the colors its designs gave it are dropped.");

        rooms.MapPost(Constants.Routes.RoomPlan, BuildPlanAsync)
            .WithName("BuildPlanFromRoom")
            .WithSummary("Reads the room's photo into a new room plan, to change in the room builder and photograph again.")
            .WithDescription("The room's AI reads the room's type, size, surfaces and objects (from the builder's catalog) from the photo; overlapping " +
                "objects are moved apart and wall-side furniture is pushed against its wall. Objects that fit nowhere are left out and listed. " +
                "The plan is linked to the room. With the real AI it is one Claude call. An optional body names the design its photos go into: " +
                "`designId` (one of the user's designs with room for another room), or a `newDesign` (`name`, `description`) made with the first photo.")
            .RequireRateLimiting(Constants.RateLimits.RemovalsPolicy);

        rooms.MapPost(Constants.Routes.RoomPlanDesign, SaveToPlanDesignAsync)
            .WithName("SaveRoomToPlanDesign")
            .WithSummary("Puts a room of a room plan's photos into the plan's own design (made, named after the plan, when it has none).")
            .RequireAuthorization();

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

        rooms.MapPost(Constants.Routes.RoomParts, FindPartsAsync)
            .WithName("FindRoomParts")
            .WithSummary("Asks the room's AI for the parts of an object (`objectIndex`), such as a chair's seat, backrest and legs, with optional `instructions` in the user's words: each part becomes a part of it.")
            .WithDescription("Starts from `baseRevision`; Claude lists the parts and SAM 3 outlines them, and the object's pixels they cover become the parts in a new label map version that becomes current. With the real AI this is paid, so it is rate-limited.")
            .RequireRateLimiting(Constants.RateLimits.RemovalsPolicy);

        rooms.MapPost(Constants.Routes.RoomAnalysis, StartAnalysisAsync)
            .WithName("StartRoomAnalysis")
            .WithSummary("Starts the analysis of a room that waits for it (a photo made by the room builder).")
            .WithDescription("The room's AI (fake or real, chosen when it was made) analyzes it as after an upload; follow it with `GET /api/rooms/{id}` or the live updates.");

        rooms.MapPost(Constants.Routes.RoomMerges, MergePartAsync)
            .WithName("MergeRoomPart")
            .WithSummary("Merges a part (`objectIndex`) back into the object it is a part of: its pixels become the object's again.")
            .WithDescription("Starts from `baseRevision` and stores the result as a new label map version that becomes current; the part stays listed, so undo brings it back.");

        rooms.MapPut(Constants.Routes.RoomCurrentLabels, UseLabelMapRevisionAsync)
            .WithName("UseRoomLabelMapRevision")
            .WithSummary("Makes a stored label map version current again (undo or redo of an outline fix).");

        api.MapGet(Constants.Routes.Ai, (IAiProviders aiProviders) => TypedResults.Ok(AiAvailabilityResponse.From(aiProviders.Availability)))
            .WithTags(Constants.OpenApi.RoomsTag)
            .WithName("GetAiAvailability")
            .WithSummary("Says which AI the uploads use, and whether each upload may choose between the fake and the real AI.");

        return api;
    }

    /// <summary><c>POST /api/rooms/{id}/plan</c>: the room's photo read into a new plan.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="request">The optional body: the design the plan's photos go into, or a new one.</param>
    /// <param name="build">The use case.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    private static async Task<Results<Created<PhotoPlanResponse>, ProblemHttpResult, NotFound>> BuildPlanAsync(Guid id, BuildPlanRequest? request,
        BuildPlanFromRoom build, CancellationToken cancellationToken)
    {
        var outcome = await build.HandleAsync(id, request?.DesignId, request?.NewDesign?.ToDomain(), cancellationToken);

        if (outcome is null)
        {
            return TypedResults.NotFound();
        }

        return outcome.Succeeded
            ? TypedResults.Created(Constants.Routes.PlanUrl(outcome.Value.Plan.Id), PhotoPlanResponse.From(outcome.Value))
            : Problems.BadRequest(outcome.Error, Constants.Titles.PhotoPlanFailed);
    }

    /// <summary><c>POST /api/rooms</c>: validates and stores the photo, then creates the room.</summary>
    /// <param name="photo">The uploaded file from the <c>photo</c> form field.</param>
    /// <param name="scope">The optional <c>scope</c> form field: what the AI looks for.</param>
    /// <param name="ai">The optional <c>ai</c> form field: Fake or Real.</param>
    /// <param name="analyze">The optional <c>analyze</c> form field: false leaves the room waiting instead of starting its analysis.</param>
    /// <param name="aiProviders">Says which AI modes may be used.</param>
    /// <param name="uploadRoom">The use case that processes and stores the photo.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>201 with the new room, 400 for an unusable photo, or 413 when it is too large.</returns>
    private static async Task<Results<Created<RoomResponse>, ProblemHttpResult>> UploadAsync(
        IFormFile photo, [FromForm] string? scope, [FromForm] string? ai, [FromForm] bool? analyze, UploadRoom uploadRoom, IAiProviders aiProviders,
        CancellationToken cancellationToken)
    {
        if (AiModeChoice.TryResolve(ai, aiProviders.Availability, out var aiMode) is { } problem)
        {
            return problem;
        }

        var analysisScope = AnalysisScope.WholeRoom;

        if (!string.IsNullOrWhiteSpace(scope) && (!Enum.TryParse(scope, ignoreCase: true, out analysisScope) || !Enum.IsDefined(analysisScope)))
        {
            return Problems.BadRequest(Constants.Messages.UnknownScope(scope, Enum.GetNames<AnalysisScope>()), Constants.Titles.InvalidScope);
        }

        if (photo.Length > Application.Constants.Photos.MaxUploadBytes)
        {
            return TypedResults.Problem(
                Constants.Messages.PhotoTooLarge,
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: Constants.Titles.PhotoTooLarge);
        }

        await using var content = photo.OpenReadStream();
        var result = await uploadRoom.HandleAsync(content, cancellationToken, analysisScope, aiMode, analyze ?? true);

        if (!result.Succeeded)
        {
            return Problems.BadRequest(result.Error, Constants.Titles.InvalidPhoto);
        }

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
            id, request.BaseRevision, request.Label, request.Category, request.Strokes ?? [], request.ParentIndex, cancellationToken);

        return ToResponse(outcome, Constants.Titles.NewObjectFailed);
    }

    /// <summary><c>POST /api/rooms/{id}/parts</c>: asks the AI for an object's parts.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="request">The label map version the editor shows and the object.</param>
    /// <param name="findParts">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the room and its new label map version, 400 when no parts can be found, or 404.</returns>
    private static async Task<Results<Ok<RoomResponse>, ProblemHttpResult, NotFound>> FindPartsAsync(
        Guid id, FindPartsRequest request, FindRoomParts findParts, CancellationToken cancellationToken)
    {
        var outcome = await findParts.FindAsync(id, request.BaseRevision, request.ObjectIndex, request.Instructions, cancellationToken);

        return ToResponse(outcome, Constants.Titles.PartsFailed);
    }

    /// <summary><c>POST /api/rooms/{id}/merges</c>: merges a part back into its object.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="request">The label map version the editor shows and the part.</param>
    /// <param name="mergePart">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the room and its new label map version, 400 when the part can't be merged, or 404.</returns>
    private static async Task<Results<Ok<RoomResponse>, ProblemHttpResult, NotFound>> MergePartAsync(
        Guid id, MergePartRequest request, MergeRoomPart mergePart, CancellationToken cancellationToken)
    {
        var outcome = await mergePart.MergeAsync(id, request.BaseRevision, request.ObjectIndex, cancellationToken);

        return ToResponse(outcome, Constants.Titles.MergeFailed);
    }

    /// <summary><c>GET /api/rooms/{id}/takes</c>: lists the room's photos to choose from.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="getTakes">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the photos, or 404.</returns>
    private static async Task<Results<Ok<List<PhotoTakeResponse>>, NotFound>> GetTakesAsync(Guid id, GetRoomTakes getTakes, CancellationToken cancellationToken)
    {
        var takes = await getTakes.HandleAsync(id, cancellationToken);

        return takes is null ? TypedResults.NotFound() : TypedResults.Ok(takes.Select(PhotoTakeResponse.From).ToList());
    }

    /// <summary><c>GET /api/rooms/{id}/takes/{takeId}/photo</c>: streams one of the room's photos to choose from.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="takeId">The photo's id.</param>
    /// <param name="getTakes">The use case that lists the photos.</param>
    /// <param name="storage">Where the photos are stored.</param>
    /// <param name="response">Used to set the caching headers.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the JPEG, or 404 when the room or the photo is missing.</returns>
    private static async Task<Results<FileStreamHttpResult, NotFound>> GetTakePhotoAsync(
        Guid id, Guid takeId, GetRoomTakes getTakes, IFileStorage storage, HttpResponse response, CancellationToken cancellationToken)
    {
        var takes = await getTakes.HandleAsync(id, cancellationToken);
        var take = takes?.FirstOrDefault(t => t.Id == takeId);

        return take is null ? TypedResults.NotFound() : await StreamImmutableAsync(take.Key, storage, response, cancellationToken);
    }

    /// <summary><c>POST /api/rooms/{id}/takes/{takeId}/change</c>: has the AI make a changed version of a photo.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="takeId">The photo to change.</param>
    /// <param name="request">The change, in the user's words.</param>
    /// <param name="changeTake">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>201 with the new photo, 400 when it couldn't be made, or 404.</returns>
    private static async Task<Results<Created<PhotoTakeResponse>, ProblemHttpResult, NotFound>> ChangeTakeAsync(
        Guid id, Guid takeId, ChangeTakeRequest request, ChangeRoomTake changeTake, CancellationToken cancellationToken)
    {
        var outcome = await changeTake.HandleAsync(id, takeId, request.Instructions, cancellationToken);

        if (outcome is null)
        {
            return TypedResults.NotFound();
        }

        return outcome.Succeeded
            ? TypedResults.Created(Constants.Routes.TakePhotoUrl(id, outcome.Value.Id), PhotoTakeResponse.From(outcome.Value))
            : Problems.BadRequest(outcome.Error, Constants.Titles.ChangeFailed);
    }

    /// <summary><c>POST /api/rooms/{id}/takes/{takeId}/choose</c>: makes a photo the room's photo.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="takeId">The photo.</param>
    /// <param name="chooseTake">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the room, 400 while its photo is being analyzed, or 404.</returns>
    private static async Task<Results<Ok<RoomResponse>, ProblemHttpResult, NotFound>> ChooseTakeAsync(
        Guid id, Guid takeId, ChooseRoomTake chooseTake, CancellationToken cancellationToken)
    {
        var outcome = await chooseTake.HandleAsync(id, takeId, cancellationToken);

        return ToResponse(outcome, Constants.Titles.ChooseFailed);
    }

    /// <summary><c>POST /api/rooms/{id}/plan-design</c>: puts a room of a plan's photos into the plan's design.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="saveToPlanDesign">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the design's id, 400 when the room has no plan or the design is full, or 404.</returns>
    private static async Task<Results<Ok<PlanDesignResponse>, ProblemHttpResult, NotFound>> SaveToPlanDesignAsync(
        Guid id, SaveRoomToPlanDesign saveToPlanDesign, CancellationToken cancellationToken)
    {
        var outcome = await saveToPlanDesign.HandleAsync(id, cancellationToken);

        if (outcome is null)
        {
            return TypedResults.NotFound();
        }

        return outcome.Succeeded
            ? TypedResults.Ok(new PlanDesignResponse(outcome.Value))
            : Problems.BadRequest(outcome.Error, Constants.Titles.PlanDesignFailed);
    }

    /// <summary><c>POST /api/rooms/{id}/analysis</c>: starts the analysis of a room that waits for it.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="startAnalysis">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the room (now queued), 400 when it isn't waiting, or 404.</returns>
    private static async Task<Results<Ok<RoomResponse>, ProblemHttpResult, NotFound>> StartAnalysisAsync(
        Guid id, StartRoomAnalysis startAnalysis, CancellationToken cancellationToken)
    {
        var outcome = await startAnalysis.HandleAsync(id, cancellationToken);

        return ToResponse(outcome, Constants.Titles.AnalysisFailed);
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
        {
            return TypedResults.NotFound();
        }

        return result.Succeeded
            ? TypedResults.Ok(RoomResponse.From(result.Value))
            : Problems.BadRequest(result.Error, title);
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
        {
            return TypedResults.NotFound();
        }

        // These files never change, so browsers can keep them for good. Vary on Origin so a copy
        // cached by a plain <img> is never reused for a CORS request (the WebGL editor), which would fail.
        response.Headers.CacheControl = Constants.Http.ImmutableCache;
        response.Headers.Vary = Constants.Http.Origin;

        return TypedResults.Stream(file.Content, file.ContentType);
    }
}
