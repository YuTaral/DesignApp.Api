using DesignApp.Application.Designs;
using DesignApp.Domain.Common;
using DesignApp.Domain.Designs;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DesignApp.Api.Designs;

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
    private static async Task<Ok<List<DesignResponse>>> ListAsync(GetDesigns getDesigns, CancellationToken cancellationToken) =>
        TypedResults.Ok((await getDesigns.AllAsync(cancellationToken)).Select(DesignResponse.From).ToList());

    /// <summary><c>POST /api/designs</c>.</summary>
    /// <param name="request">The first room, its colors, and the design's name and description.</param>
    /// <param name="createDesign">Creates the design.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>201 with the design, 404 when the account has no such room, or 400 for invalid input.</returns>
    private static async Task<Results<Created<DesignResponse>, NotFound>> CreateAsync(
        CreateDesignRequest request, CreateDesign createDesign, CancellationToken cancellationToken)
    {
        var design = await createDesign.HandleAsync(
            request.RoomId, request.Name ?? "", request.Description, ToEdits(request.Edits), cancellationToken);
        return design is null
            ? TypedResults.NotFound()
            : TypedResults.Created(Constants.Routes.DesignUrl(design.Id), DesignResponse.From(design));
    }

    /// <summary><c>GET /api/designs/{id}</c>.</summary>
    /// <param name="id">The design.</param>
    /// <param name="getDesigns">Reads the design.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    private static async Task<Results<Ok<DesignResponse>, NotFound>> GetAsync(Guid id, GetDesigns getDesigns, CancellationToken cancellationToken) =>
        Found(await getDesigns.OneAsync(id, cancellationToken));

    /// <summary><c>PUT /api/designs/{id}</c>.</summary>
    /// <param name="id">The design.</param>
    /// <param name="request">The new name and description.</param>
    /// <param name="editDesign">Saves them.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    private static async Task<Results<Ok<DesignResponse>, NotFound>> EditAsync(
        Guid id, EditDesignRequest request, EditDesignDetails editDesign, CancellationToken cancellationToken) =>
        Found(await editDesign.HandleAsync(id, request.Name ?? "", request.Description, cancellationToken));

    /// <summary><c>DELETE /api/designs/{id}</c>.</summary>
    /// <param name="id">The design.</param>
    /// <param name="deleteDesign">Deletes it with its rooms.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    private static async Task<Results<NoContent, NotFound>> DeleteAsync(Guid id, DeleteDesign deleteDesign, CancellationToken cancellationToken) =>
        await deleteDesign.HandleAsync(id, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();

    /// <summary><c>POST /api/designs/{id}/rooms</c>.</summary>
    /// <param name="id">The design.</param>
    /// <param name="request">The room and its colors.</param>
    /// <param name="addRoom">Adds it.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    private static async Task<Results<Ok<DesignResponse>, NotFound>> AddRoomAsync(
        Guid id, AddDesignRoomRequest request, AddRoomToDesign addRoom, CancellationToken cancellationToken) =>
        Found(await addRoom.HandleAsync(id, request.RoomId, ToEdits(request.Edits), cancellationToken));

    /// <summary><c>PUT /api/designs/{id}/rooms/{roomId}</c>.</summary>
    /// <param name="id">The design.</param>
    /// <param name="roomId">The room.</param>
    /// <param name="request">Its new colors.</param>
    /// <param name="saveColors">Saves them.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    private static async Task<Results<Ok<DesignResponse>, NotFound>> SaveRoomColorsAsync(
        Guid id, Guid roomId, RoomColorsRequest request, SaveRoomColors saveColors, CancellationToken cancellationToken) =>
        Found(await saveColors.HandleAsync(id, roomId, ToEdits(request.Edits), cancellationToken));

    /// <summary><c>DELETE /api/designs/{id}/rooms/{roomId}</c>.</summary>
    /// <param name="id">The design.</param>
    /// <param name="roomId">The room.</param>
    /// <param name="removeRoom">Removes and deletes it.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    private static async Task<Results<Ok<DesignResponse>, NotFound>> RemoveRoomAsync(
        Guid id, Guid roomId, RemoveRoomFromDesign removeRoom, CancellationToken cancellationToken) =>
        Found(await removeRoom.HandleAsync(id, roomId, cancellationToken));

    /// <summary>200 with the design, or 404 when there is none.</summary>
    /// <param name="design">The design, if found.</param>
    private static Results<Ok<DesignResponse>, NotFound> Found(Design? design) =>
        design is null ? TypedResults.NotFound() : TypedResults.Ok(DesignResponse.From(design));

    /// <summary>The colors of a request as domain edits; an invalid color or strength is a 400.</summary>
    /// <param name="edits">The colors as sent.</param>
    /// <exception cref="DomainException">A color isn't #RRGGBB or a strength isn't between 0 and 1.</exception>
    private static List<EditOperation> ToEdits(List<EditRequest>? edits) =>
        [.. (edits ?? []).Select(e => new RecolorEdit(e.ObjectIndex, HexColor.Parse(e.Color ?? ""), e.Strength))];
}

/// <summary>Body of <c>POST /api/designs</c>.</summary>
/// <param name="RoomId">The design's first room.</param>
/// <param name="Name">What to call the design (1 to 80 characters).</param>
/// <param name="Description">An optional short description (up to 300 characters).</param>
/// <param name="Edits">The room's colors, at most one per object.</param>
internal sealed record CreateDesignRequest(Guid RoomId, string? Name, string? Description, List<EditRequest>? Edits);

/// <summary>Body of <c>PUT /api/designs/{id}</c>.</summary>
/// <param name="Name">The design's new name.</param>
/// <param name="Description">Its new description; empty or missing for none.</param>
internal sealed record EditDesignRequest(string? Name, string? Description);

/// <summary>Body of <c>POST /api/designs/{id}/rooms</c>.</summary>
/// <param name="RoomId">The room to add.</param>
/// <param name="Edits">Its colors; none for a room still being analyzed.</param>
internal sealed record AddDesignRoomRequest(Guid RoomId, List<EditRequest>? Edits);

/// <summary>Body of <c>PUT /api/designs/{id}/rooms/{roomId}</c>.</summary>
/// <param name="Edits">The room's new colors, replacing the old ones.</param>
internal sealed record RoomColorsRequest(List<EditRequest>? Edits);

/// <summary>One recolor, as sent and as returned.</summary>
/// <param name="ObjectIndex">The object, as in the room's objects and label map.</param>
/// <param name="Color">The new color as #RRGGBB.</param>
/// <param name="Strength">How strongly it is applied, from 0 to 1.</param>
internal sealed record EditRequest(int ObjectIndex, string? Color, double Strength);

/// <summary>A design as the API returns it.</summary>
/// <param name="Id">The design's id.</param>
/// <param name="Name">The design's name.</param>
/// <param name="Description">Its short description; empty when there is none.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="UpdatedAt">When it or one of its rooms was last changed.</param>
/// <param name="Rooms">Its rooms, in the order they were added.</param>
internal sealed record DesignResponse(
    Guid Id, string Name, string Description, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, List<DesignRoomResponse> Rooms)
{
    /// <summary>The response for a design.</summary>
    /// <param name="design">The design.</param>
    public static DesignResponse From(Design design) => new(
        design.Id,
        design.Name,
        design.Description,
        design.CreatedAt,
        design.UpdatedAt,
        [.. design.Rooms.OrderBy(r => r.Position).Select(DesignRoomResponse.From)]);
}

/// <summary>One room of a design.</summary>
/// <param name="RoomId">The room.</param>
/// <param name="PhotoUrl">Its photo, for a thumbnail.</param>
/// <param name="Edits">Its colors.</param>
internal sealed record DesignRoomResponse(Guid RoomId, string PhotoUrl, List<EditRequest> Edits)
{
    /// <summary>The response for a room of a design.</summary>
    /// <param name="room">The room.</param>
    public static DesignRoomResponse From(DesignRoom room) => new(
        room.RoomId,
        Constants.Routes.RoomPhotoUrl(room.RoomId),
        [.. room.Edits.OfType<RecolorEdit>().Select(e => new EditRequest(e.ObjectIndex, e.Color.Value, e.Strength))]);
}
