using DesignApp.Api.Requests;
using DesignApp.Api.Responses;
using DesignApp.Application.UseCases;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DesignApp.Api.Endpoints;

/// <summary>
/// The <c>/api/designs</c> endpoints. A design has a name, a short description and one or more
/// rooms, each with its own colors. Designs need an account; a guest gets 401.
/// </summary>
internal static class DesignEndpoints
{
    /// <summary>Maps the design endpoints under <c>/designs</c>.</summary>
    /// <param name="api">The <c>/api</c> route group.</param>
    /// <returns>The same group, for chaining.</returns>
    public static RouteGroupBuilder MapDesignEndpoints(this RouteGroupBuilder api)
    {
        var designs = api.MapGroup(Constants.Routes.Designs).WithTags(Constants.OpenApi.DesignsTag).RequireAuthorization();

        designs.MapGet(Constants.Routes.Root, ListAsync)
            .WithName("ListDesigns")
            .WithSummary("Lists the account's designs with their rooms, most recently changed first.");

        designs.MapPost(Constants.Routes.Root, CreateAsync)
            .WithName("CreateDesign")
            .WithSummary("Creates a design from one of the account's rooms and its colors.")
            .WithDescription($"The room must not be in another design. Uploads that are never added to a design are deleted after {Application.Constants.Rooms.UnsavedLifetime.TotalHours} hours.");

        designs.MapGet(Constants.Routes.ById, GetAsync)
            .WithName("GetDesign")
            .WithSummary("Returns one design with its rooms and their colors.");

        designs.MapPut(Constants.Routes.ById, EditAsync)
            .WithName("EditDesign")
            .WithSummary("Changes a design's name and description.");

        designs.MapDelete(Constants.Routes.ById, DeleteAsync)
            .WithName("DeleteDesign")
            .WithSummary("Deletes a design together with its rooms, their photos and outlines.");

        designs.MapPost(Constants.Routes.DesignRooms, AddRoomAsync)
            .WithName("AddDesignRoom")
            .WithSummary("Adds one of the account's rooms to a design, optionally with its colors (none while it is still analyzed).");

        designs.MapPut(Constants.Routes.DesignRoom, SaveRoomColorsAsync)
            .WithName("SaveDesignRoomColors")
            .WithSummary("Replaces the colors of one room of a design.");

        designs.MapDelete(Constants.Routes.DesignRoom, RemoveRoomAsync)
            .WithName("RemoveDesignRoom")
            .WithSummary("Takes a room out of a design and deletes it. The last room can't be removed; delete the design instead.");

        return api;
    }

    /// <summary><c>GET /api/designs</c>.</summary>
    /// <param name="getDesigns">Reads the designs.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    private static async Task<Ok<List<DesignResponse>>> ListAsync(GetDesigns getDesigns, CancellationToken cancellationToken)
    {
        var designs = await getDesigns.AllAsync(cancellationToken);
        var photoVersions = await getDesigns.PhotoVersionsAsync(cancellationToken);
        return TypedResults.Ok(designs.Select(d => DesignResponse.From(d, photoVersions)).ToList());
    }

    /// <summary><c>POST /api/designs</c>.</summary>
    /// <param name="request">The first room, its colors, and the design's name and description.</param>
    /// <param name="createDesign">Creates the design.</param>
    /// <param name="getDesigns">Reads the rooms' photo versions.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>201 with the design, 404 when the account has no such room, or 400 for invalid input.</returns>
    private static async Task<Results<Created<DesignResponse>, NotFound>> CreateAsync(
        CreateDesignRequest request, CreateDesign createDesign, GetDesigns getDesigns, CancellationToken cancellationToken)
    {
        var design = await createDesign.HandleAsync(
            request.RoomId, request.Name ?? "", request.Description, ToEdits(request.Edits), cancellationToken);
        if (design is null)
            return TypedResults.NotFound();
        var photoVersions = await getDesigns.PhotoVersionsAsync(cancellationToken);
        return TypedResults.Created(Constants.Routes.DesignUrl(design.Id), DesignResponse.From(design, photoVersions));
    }

    /// <summary><c>GET /api/designs/{id}</c>.</summary>
    /// <param name="id">The design.</param>
    /// <param name="getDesigns">Reads the design.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    private static async Task<Results<Ok<DesignResponse>, NotFound>> GetAsync(Guid id, GetDesigns getDesigns, CancellationToken cancellationToken)
    {
        var design = await getDesigns.OneAsync(id, cancellationToken);
        return await FoundAsync(design, getDesigns, cancellationToken);
    }

    /// <summary><c>PUT /api/designs/{id}</c>.</summary>
    /// <param name="id">The design.</param>
    /// <param name="request">The new name and description.</param>
    /// <param name="editDesign">Saves them.</param>
    /// <param name="getDesigns">Reads the rooms' photo versions.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    private static async Task<Results<Ok<DesignResponse>, NotFound>> EditAsync(
        Guid id, EditDesignRequest request, EditDesignDetails editDesign, GetDesigns getDesigns, CancellationToken cancellationToken)
    {
        var design = await editDesign.HandleAsync(id, request.Name ?? "", request.Description, cancellationToken);
        return await FoundAsync(design, getDesigns, cancellationToken);
    }

    /// <summary><c>DELETE /api/designs/{id}</c>.</summary>
    /// <param name="id">The design.</param>
    /// <param name="deleteDesign">Deletes it with its rooms.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    private static async Task<Results<NoContent, NotFound>> DeleteAsync(Guid id, DeleteDesign deleteDesign, CancellationToken cancellationToken)
    {
        var deleted = await deleteDesign.HandleAsync(id, cancellationToken);
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    /// <summary><c>POST /api/designs/{id}/rooms</c>.</summary>
    /// <param name="id">The design.</param>
    /// <param name="request">The room and its colors.</param>
    /// <param name="addRoom">Adds it.</param>
    /// <param name="getDesigns">Reads the rooms' photo versions.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    private static async Task<Results<Ok<DesignResponse>, NotFound>> AddRoomAsync(
        Guid id, AddDesignRoomRequest request, AddRoomToDesign addRoom, GetDesigns getDesigns, CancellationToken cancellationToken)
    {
        var design = await addRoom.HandleAsync(id, request.RoomId, ToEdits(request.Edits), cancellationToken);
        return await FoundAsync(design, getDesigns, cancellationToken);
    }

    /// <summary><c>PUT /api/designs/{id}/rooms/{roomId}</c>.</summary>
    /// <param name="id">The design.</param>
    /// <param name="roomId">The room.</param>
    /// <param name="request">Its new colors.</param>
    /// <param name="saveColors">Saves them.</param>
    /// <param name="getDesigns">Reads the rooms' photo versions.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    private static async Task<Results<Ok<DesignResponse>, NotFound>> SaveRoomColorsAsync(
        Guid id, Guid roomId, RoomColorsRequest request, SaveRoomColors saveColors, GetDesigns getDesigns, CancellationToken cancellationToken)
    {
        var design = await saveColors.HandleAsync(id, roomId, ToEdits(request.Edits), cancellationToken);
        return await FoundAsync(design, getDesigns, cancellationToken);
    }

    /// <summary><c>DELETE /api/designs/{id}/rooms/{roomId}</c>.</summary>
    /// <param name="id">The design.</param>
    /// <param name="roomId">The room.</param>
    /// <param name="removeRoom">Removes and deletes it.</param>
    /// <param name="getDesigns">Reads the rooms' photo versions.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    private static async Task<Results<Ok<DesignResponse>, NotFound>> RemoveRoomAsync(
        Guid id, Guid roomId, RemoveRoomFromDesign removeRoom, GetDesigns getDesigns, CancellationToken cancellationToken)
    {
        var design = await removeRoom.HandleAsync(id, roomId, cancellationToken);
        return await FoundAsync(design, getDesigns, cancellationToken);
    }

    /// <summary>200 with the design (its rooms with the photo version each is on), or 404 when there is none.</summary>
    /// <param name="design">The design, if found.</param>
    /// <param name="getDesigns">Reads the rooms' photo versions.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    private static async Task<Results<Ok<DesignResponse>, NotFound>> FoundAsync(Design? design, GetDesigns getDesigns, CancellationToken cancellationToken)
    {
        if (design is null)
            return TypedResults.NotFound();
        var photoVersions = await getDesigns.PhotoVersionsAsync(cancellationToken);
        return TypedResults.Ok(DesignResponse.From(design, photoVersions));
    }

    /// <summary>The colors of a request as domain edits; an invalid color or strength is a 400.</summary>
    /// <param name="edits">The colors as sent.</param>
    /// <exception cref="DomainException">A color isn't #RRGGBB or a strength isn't between 0 and 1.</exception>
    private static List<EditOperation> ToEdits(List<EditRequest>? edits)
    {
        var sent = edits ?? [];
        return [.. sent.Select(e => new RecolorEdit(e.ObjectIndex, HexColor.Parse(e.Color ?? ""), e.Strength))];
    }
}
