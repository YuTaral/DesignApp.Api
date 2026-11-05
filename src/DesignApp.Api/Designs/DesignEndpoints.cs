using DesignApp.Application.Designs;
using DesignApp.Domain.Common;
using DesignApp.Domain.Designs;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DesignApp.Api.Designs;

/// <summary>
/// The <c>/api/designs</c> endpoints: save a room's recolors under a name, list, open, change and
/// delete them. Saving needs an account; a guest gets 401.
/// </summary>
internal static class DesignEndpoints
{
    /// <summary>Maps the design endpoints under <c>/designs</c>.</summary>
    /// <param name="api">The <c>/api</c> route group.</param>
    /// <returns>The same group, for chaining.</returns>
    public static RouteGroupBuilder MapDesignEndpoints(this RouteGroupBuilder api)
    {
        var designs = api.MapGroup("/designs").WithTags("Designs").RequireAuthorization();

        designs.MapGet("/", ListAsync)
            .WithName("ListDesigns")
            .WithSummary("Lists the account's designs, most recently changed first.");

        designs.MapPost("/", CreateAsync)
            .WithName("CreateDesign")
            .WithSummary("Saves a room's recolors as a new design. The room must be the account's and analyzed.");

        designs.MapGet("/{id:guid}", GetAsync)
            .WithName("GetDesign")
            .WithSummary("Returns one design with its recolors.");

        designs.MapPut("/{id:guid}", UpdateAsync)
            .WithName("UpdateDesign")
            .WithSummary("Renames a design and replaces its recolors.");

        designs.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeleteDesign")
            .WithSummary("Deletes a design. The room and its photo stay.");

        return api;
    }

    /// <summary><c>GET /api/designs</c>.</summary>
    /// <param name="getDesigns">Reads the designs.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    private static async Task<Ok<List<DesignResponse>>> ListAsync(GetDesigns getDesigns, CancellationToken cancellationToken) =>
        TypedResults.Ok((await getDesigns.AllAsync(cancellationToken)).Select(DesignResponse.From).ToList());

    /// <summary><c>POST /api/designs</c>.</summary>
    /// <param name="request">The room, name and recolors.</param>
    /// <param name="createDesign">Saves the design.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>201 with the design, 404 when the account has no such room, or 400 for invalid edits.</returns>
    private static async Task<Results<Created<DesignResponse>, NotFound>> CreateAsync(
        CreateDesignRequest request, CreateDesign createDesign, CancellationToken cancellationToken)
    {
        var design = await createDesign.HandleAsync(request.RoomId, request.Name ?? "", ToEdits(request.Edits), cancellationToken);
        return design is null
            ? TypedResults.NotFound()
            : TypedResults.Created($"/api/designs/{design.Id}", DesignResponse.From(design));
    }

    /// <summary><c>GET /api/designs/{id}</c>.</summary>
    /// <param name="id">The design.</param>
    /// <param name="getDesigns">Reads the design.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    private static async Task<Results<Ok<DesignResponse>, NotFound>> GetAsync(Guid id, GetDesigns getDesigns, CancellationToken cancellationToken) =>
        await getDesigns.OneAsync(id, cancellationToken) is { } design
            ? TypedResults.Ok(DesignResponse.From(design))
            : TypedResults.NotFound();

    /// <summary><c>PUT /api/designs/{id}</c>.</summary>
    /// <param name="id">The design.</param>
    /// <param name="request">The new name and recolors.</param>
    /// <param name="updateDesign">Saves the change.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    private static async Task<Results<Ok<DesignResponse>, NotFound>> UpdateAsync(
        Guid id, UpdateDesignRequest request, UpdateDesign updateDesign, CancellationToken cancellationToken) =>
        await updateDesign.HandleAsync(id, request.Name ?? "", ToEdits(request.Edits), cancellationToken) is { } design
            ? TypedResults.Ok(DesignResponse.From(design))
            : TypedResults.NotFound();

    /// <summary><c>DELETE /api/designs/{id}</c>.</summary>
    /// <param name="id">The design.</param>
    /// <param name="deleteDesign">Deletes it.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    private static async Task<Results<NoContent, NotFound>> DeleteAsync(Guid id, DeleteDesign deleteDesign, CancellationToken cancellationToken) =>
        await deleteDesign.HandleAsync(id, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();

    /// <summary>The recolors of a request as domain edits; an invalid color or strength is a 400.</summary>
    /// <param name="edits">The recolors as sent.</param>
    /// <exception cref="DomainException">A color isn't #RRGGBB or a strength isn't between 0 and 1.</exception>
    private static List<EditOperation> ToEdits(List<EditRequest>? edits) =>
        [.. (edits ?? []).Select(e => new RecolorEdit(e.ObjectIndex, HexColor.Parse(e.Color ?? ""), e.Strength))];
}

/// <summary>Body of <c>POST /api/designs</c>.</summary>
/// <param name="RoomId">The restyled room.</param>
/// <param name="Name">What to call the design (1 to 80 characters).</param>
/// <param name="Edits">The recolors, at most one per object.</param>
internal sealed record CreateDesignRequest(Guid RoomId, string? Name, List<EditRequest>? Edits);

/// <summary>Body of <c>PUT /api/designs/{id}</c>.</summary>
/// <param name="Name">The design's new name.</param>
/// <param name="Edits">Its new recolors, replacing the old ones.</param>
internal sealed record UpdateDesignRequest(string? Name, List<EditRequest>? Edits);

/// <summary>One recolor, as sent and as returned.</summary>
/// <param name="ObjectIndex">The object, as in the room's objects and label map.</param>
/// <param name="Color">The new color as #RRGGBB.</param>
/// <param name="Strength">How strongly it is applied, from 0 to 1.</param>
internal sealed record EditRequest(int ObjectIndex, string? Color, double Strength);

/// <summary>A design as the API returns it.</summary>
/// <param name="Id">The design's id.</param>
/// <param name="RoomId">The restyled room.</param>
/// <param name="Name">The design's name.</param>
/// <param name="UpdatedAt">When it was last saved.</param>
/// <param name="PhotoUrl">The room's photo, for a thumbnail.</param>
/// <param name="Edits">The recolors.</param>
internal sealed record DesignResponse(Guid Id, Guid RoomId, string Name, DateTimeOffset UpdatedAt, string PhotoUrl, List<EditRequest> Edits)
{
    /// <summary>The response for a design.</summary>
    /// <param name="design">The design.</param>
    public static DesignResponse From(Design design) => new(
        design.Id,
        design.RoomId,
        design.Name,
        design.UpdatedAt,
        $"/api/rooms/{design.RoomId}/photo",
        [.. design.Edits.OfType<RecolorEdit>().Select(e => new EditRequest(e.ObjectIndex, e.Color.Value, e.Strength))]);
}
