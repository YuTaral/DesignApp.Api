using DesignApp.Api.Helpers;
using DesignApp.Api.Responses;
using DesignApp.Application.Interfaces;
using DesignApp.Application.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace DesignApp.Api.Endpoints;

/// <summary>
/// The <c>/api/library</c> endpoints: the built-in designs, analyzed ahead of time. Opening a room copies it into a
/// room of the visitor, Ready at once, so Claude and SAM 3 never run for it.
/// </summary>
internal static class LibraryEndpoints
{
    /// <summary>Maps the library endpoints under <c>/library</c>.</summary>
    /// <param name="api">The <c>/api</c> route group.</param>
    /// <returns>The same group, for chaining.</returns>
    public static RouteGroupBuilder MapLibraryEndpoints(this RouteGroupBuilder api)
    {
        var library = api.MapGroup(Constants.Routes.Library).WithTags(Constants.OpenApi.LibraryTag);

        library.MapGet(Constants.Routes.Root, (ILibrary designs) => TypedResults.Ok(designs.Designs.Select(LibraryDesignResponse.From).ToList()))
            .WithName("ListLibrary")
            .WithSummary("Lists the built-in designs with their rooms, ready to restyle without an upload.");

        library.MapGet(Constants.Routes.LibraryLayoutPhoto, GetPhoto)
            .WithName("GetLibraryLayoutPhoto")
            .WithSummary("Returns the photo of a library room's layout.");

        library.MapPost(Constants.Routes.LibraryLayoutCopy, CopyRoomAsync)
            .WithName("CopyLibraryRoom")
            .WithSummary("Copies one layout of a library room into a new room of the visitor, already analyzed.")
            .WithDescription("No AI runs. Like an upload, the copy is deleted unless it is added to a design. " +
                "An optional `ai` query (`Fake` or `Real`, where `GET /api/ai` allows it) sets the AI that removing objects uses.")
            .RequireRateLimiting(Constants.RateLimits.UploadsPolicy);

        library.MapPost(Constants.Routes.LibraryDesignCopy, CopyDesignAsync)
            .WithName("CopyLibraryDesign")
            .WithSummary("Copies every room of a library design, in its first layout, into a new design of the account.")
            .WithDescription("No AI runs. An optional `ai` query works as for a single room.")
            .RequireAuthorization()
            .RequireRateLimiting(Constants.RateLimits.UploadsPolicy);

        return api;
    }

    /// <summary><c>GET /api/library/{designId}/{roomId}/{layoutId}/photo</c>.</summary>
    /// <param name="designId">The library design's id.</param>
    /// <param name="roomId">The room's id inside it.</param>
    /// <param name="layoutId">The layout's id inside the room.</param>
    /// <param name="library">The library.</param>
    /// <param name="response">Used to set the caching headers.</param>
    /// <returns>200 with the JPEG, or 404.</returns>
    private static Results<FileStreamHttpResult, NotFound> GetPhoto(string designId, string roomId, string layoutId, ILibrary library, HttpResponse response)
    {
        if (library.FindDesign(designId)?.FindRoom(roomId)?.FindLayout(layoutId) is null)
            return TypedResults.NotFound();
        response.Headers.CacheControl = Constants.Http.LibraryCache;
        return TypedResults.Stream(library.OpenPhoto(designId, roomId, layoutId), Application.Constants.ContentTypes.Jpeg);
    }

    /// <summary><c>POST /api/library/{designId}/{roomId}/{layoutId}/copy</c>.</summary>
    /// <param name="designId">The library design's id.</param>
    /// <param name="roomId">The room's id inside it.</param>
    /// <param name="layoutId">The layout's id inside the room.</param>
    /// <param name="ai">The optional <c>ai</c> query.</param>
    /// <param name="copyRoom">The use case.</param>
    /// <param name="aiProviders">Says which AI modes may be used.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>201 with the new room, 400 for an AI that can't be used, or 404 for an unknown layout.</returns>
    private static async Task<Results<Created<RoomResponse>, ProblemHttpResult, NotFound>> CopyRoomAsync(
        string designId, string roomId, string layoutId, [FromQuery] string? ai, CopyLibraryRoom copyRoom, IAiProviders aiProviders,
        CancellationToken cancellationToken)
    {
        if (AiModeChoice.TryResolve(ai, aiProviders.Availability, out var aiMode) is { } problem)
            return problem;
        var room = await copyRoom.HandleAsync(designId, roomId, layoutId, aiMode, cancellationToken);
        if (room is null)
            return TypedResults.NotFound();
        return TypedResults.Created(Constants.Routes.RoomUrl(room.Id), RoomResponse.From(room));
    }

    /// <summary><c>POST /api/library/{designId}/copy</c>.</summary>
    /// <param name="designId">The library design's id.</param>
    /// <param name="ai">The optional <c>ai</c> query.</param>
    /// <param name="copyDesign">The use case.</param>
    /// <param name="aiProviders">Says which AI modes may be used.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>201 with the new design, 400 for an AI that can't be used, or 404 for an unknown library design.</returns>
    private static async Task<Results<Created<DesignResponse>, ProblemHttpResult, NotFound>> CopyDesignAsync(
        string designId, [FromQuery] string? ai, CopyLibraryDesign copyDesign, IAiProviders aiProviders,
        CancellationToken cancellationToken)
    {
        if (AiModeChoice.TryResolve(ai, aiProviders.Availability, out var aiMode) is { } problem)
            return problem;
        var design = await copyDesign.HandleAsync(designId, aiMode, cancellationToken);
        if (design is null)
            return TypedResults.NotFound();
        return TypedResults.Created(Constants.Routes.DesignUrl(design.Id), DesignResponse.From(design, new Dictionary<Guid, int>()));
    }
}
