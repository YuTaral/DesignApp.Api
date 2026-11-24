using System.Text.Json;
using DesignApp.Api.Helpers;
using DesignApp.Api.Requests;
using DesignApp.Api.Responses;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Application.UseCases;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using static DesignApp.Domain.Constants;

namespace DesignApp.Api.Endpoints;

/// <summary>
/// The room builder: the common objects of each room type, the rooms users build (walls and objects), objects made
/// from descriptions, and photos generated from a room's 3D view.
/// </summary>
internal static class PlanEndpoints
{
    /// <summary>How the <c>boxes</c> form field of a photo request is read (camelCase JSON).</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Maps the room builder's endpoints.</summary>
    /// <param name="api">The <c>/api</c> group.</param>
    public static RouteGroupBuilder MapPlanEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet(Constants.Routes.BuilderCatalog, Catalog)
            .WithTags(Constants.OpenApi.PlansTag)
            .WithName("ListBuilderCatalog")
            .WithSummary("Lists the most common objects of a room type (`LivingRoom`, `Bedroom`…), with their usual sizes in centimetres and colors.");

        var plans = api.MapGroup(Constants.Routes.Plans).WithTags(Constants.OpenApi.PlansTag);

        plans.MapPost(Constants.Routes.Root, CreateAsync)
            .WithName("CreatePlan")
            .WithSummary("Starts a room to build: its type and walls (metres) and their colors, with no objects yet.")
            .WithDescription("An optional `ai` query (`Fake` or `Real`, where `GET /api/ai` allows it) sets the AI its descriptions and photos use. " +
                "An optional `designId` (one of the user's designs with room for another room) puts the room's photos into that design; " +
                "without one, an optional `newDesign` (`name`, `description`) is the new design they go into, made with the first photo.");
        plans.MapGet(Constants.Routes.ById, GetAsync)
            .WithName("GetPlan")
            .WithSummary("Returns a built room with its walls and objects.");
        plans.MapPut(Constants.Routes.ById, SaveAsync)
            .WithName("SavePlan")
            .WithSummary("Saves a built room as the user left it: name, walls, colors and objects (positions in centimetres from the back-left corner).");
        plans.MapPost(Constants.Routes.PlanDescribe, DescribeAsync)
            .WithName("DescribePlanObject")
            .WithSummary("Turns a description of an object into one that can be placed, with its size and color (the AI; Claude Haiku when real).")
            .WithDescription("The object isn't added to the room: place it, then save the room. With the real AI this is a paid call, so it is rate-limited.")
            .RequireRateLimiting(Constants.RateLimits.RemovalsPolicy);
        plans.MapPost(Constants.Routes.PlanGenerate, GenerateAsync)
            .WithName("GeneratePlanRoom")
            .WithSummary("Makes a realistic photo of the built room from its 3D view (multipart field `guide`, a PNG) and returns the new room made from it.")
            .WithDescription("Optional form fields: `style` (AsBuilt, Modern, Scandinavian, Boho, Industrial, Japandi, MidCentury, Farmhouse, Coastal, Minimalist, Traditional, ArtDeco) " +
                "and `instructions` (the user's own words for the AI). Each object's decorations (such as cushions on a sofa) are named with it and drawn on it; nothing else is added. The prompt is written from the plan with every size and place; FLUX Kontext on fal.ai keeps the 3D view's layout. " +
                "The new room waits for the user to start its analysis. With the real AI this is paid, so it is rate-limited.")
            .DisableAntiforgery() // the cookies are SameSite=Lax, so another site's form posts arrive without them
            .RequireRateLimiting(Constants.RateLimits.RemovalsPolicy);

        return api;
    }

    /// <summary><c>GET /api/builder/catalog/{roomType}</c>: the common objects of a room type.</summary>
    /// <param name="roomType">The room type's name.</param>
    /// <param name="catalog">The catalog.</param>
    /// <param name="response">For the cache header.</param>
    /// <returns>200 with the objects, or 400 for an unknown room type.</returns>
    private static Results<Ok<List<CatalogObjectResponse>>, ProblemHttpResult> Catalog(string roomType, IObjectCatalog catalog, HttpResponse response)
    {
        if (!Enum.TryParse<RoomType>(roomType, ignoreCase: true, out var type) || !Enum.IsDefined(type))
        {
            return Problems.BadRequest(Domain.Constants.Messages.UnknownRoomType, Constants.Titles.InvalidRequest);
        }

        response.Headers.CacheControl = Constants.Http.CatalogCache;

        return TypedResults.Ok(catalog.ForRoom(type).Select(CatalogObjectResponse.From).ToList());
    }

    /// <summary><c>POST /api/plans</c>: starts a room to build.</summary>
    /// <param name="request">Its type, walls and colors.</param>
    /// <param name="ai">The optional <c>ai</c> query.</param>
    /// <param name="createPlan">The use case.</param>
    /// <param name="aiProviders">Says which AI may be chosen.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>201 with the plan, or 400.</returns>
    /// <exception cref="DomainException">The type, walls or colors are invalid (a 400).</exception>
    private static async Task<Results<Created<PlanResponse>, ProblemHttpResult>> CreateAsync(
        CreatePlanRequest request, [FromQuery] string? ai, CreatePlan createPlan, IAiProviders aiProviders, CancellationToken cancellationToken)
    {
        if (AiModeChoice.TryResolve(ai, aiProviders.Availability, out var aiMode) is { } problem)
        {
            return problem;
        }

        var created = await createPlan.HandleAsync(request.Name ?? "", request.RoomType, request.Width, request.Length, request.Height,
            HexColor.Parse(request.WallColor ?? ""), HexColor.Parse(request.FloorColor ?? ""), request.FloorType, aiMode, request.Furnish, request.Finish?.ToDomain(),
            request.DesignId, request.NewDesign?.ToDomain(), cancellationToken);

        return created.Succeeded
            ? TypedResults.Created(Constants.Routes.PlanUrl(created.Value.Id), PlanResponse.From(created.Value))
            : Problems.BadRequest(created.Error, Constants.Titles.PlanDesignRefused);
    }

    /// <summary><c>GET /api/plans/{id}</c>: a built room.</summary>
    /// <param name="id">The plan's id.</param>
    /// <param name="getPlan">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the plan, or 404.</returns>
    private static async Task<Results<Ok<PlanResponse>, NotFound>> GetAsync(Guid id, GetPlan getPlan, CancellationToken cancellationToken)
    {
        var plan = await getPlan.HandleAsync(id, cancellationToken);

        return plan is null ? TypedResults.NotFound() : TypedResults.Ok(PlanResponse.From(plan));
    }

    /// <summary><c>PUT /api/plans/{id}</c>: saves a built room.</summary>
    /// <param name="id">The plan's id.</param>
    /// <param name="request">Its name, walls, colors and objects.</param>
    /// <param name="savePlan">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the plan, 400, or 404.</returns>
    /// <exception cref="DomainException">The walls, colors or objects are invalid (a 400).</exception>
    private static async Task<Results<Ok<PlanResponse>, NotFound>> SaveAsync(Guid id, SavePlanRequest request, SavePlan savePlan, CancellationToken cancellationToken)
    {
        var items = (request.Items ?? []).Select(ToItem).ToList();
        var outline = request.Outline is { } corners
            ? new RoomOutline(corners.Select(c => new OutlinePoint(c.X, c.Y)))
            : RoomPlan.RectangleOutline(request.Width, request.Length);
        var view = new PlanView(request.View, request.Corner, request.CameraSpot is { } spot ? new OutlinePoint(spot.X, spot.Y) : null);
        var plan = await savePlan.HandleAsync(id, request.Name ?? "", outline, request.Height,
            HexColor.Parse(request.WallColor ?? ""), HexColor.Parse(request.FloorColor ?? ""), request.FloorType, view, items, request.Finish?.ToDomain(),
            cancellationToken);

        return plan is null ? TypedResults.NotFound() : TypedResults.Ok(PlanResponse.From(plan));
    }

    /// <summary><c>POST /api/plans/{id}/describe</c>: an object from a description.</summary>
    /// <param name="id">The plan's id.</param>
    /// <param name="request">The description.</param>
    /// <param name="describe">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the object, 400 when it can't be made, or 404.</returns>
    private static async Task<Results<Ok<CatalogObjectResponse>, ProblemHttpResult, NotFound>> DescribeAsync(
        Guid id, DescribeObjectRequest request, DescribePlanObject describe, CancellationToken cancellationToken)
    {
        var outcome = await describe.HandleAsync(id, request.Description, cancellationToken);

        if (outcome is null)
        {
            return TypedResults.NotFound();
        }

        return outcome.Succeeded
            ? TypedResults.Ok(CatalogObjectResponse.From(outcome.Value))
            : Problems.BadRequest(outcome.Error, Constants.Titles.DescribeFailed);
    }

    /// <summary><c>POST /api/plans/{id}/generate</c>: a photo of the built room, as a new room.</summary>
    /// <param name="id">The plan's id.</param>
    /// <param name="guide">The room's 3D view (PNG).</param>
    /// <param name="style">The optional <c>style</c> form field: the photo's interior style.</param>
    /// <param name="instructions">The optional <c>instructions</c> form field: the user's own words for the AI.</param>
    /// <param name="seed">The optional <c>seed</c> form field: makes the photo repeatable, to compare changes fairly.</param>
    /// <param name="boxes">The optional <c>boxes</c> form field: a JSON list of each object's box in the 3D view (<see cref="ItemBoxRequest"/>).</param>
    /// <param name="view">
    /// The optional <c>view</c> form field: the number of the wall the photo looks at, when it isn't the room's own (the
    /// guide must be drawn from it).
    /// </param>
    /// <param name="corner">The optional <c>corner</c> form field, with <c>view</c>: where the camera stands (<c>None</c> when missing).</param>
    /// <param name="roomId">
    /// The optional <c>roomId</c> form field: the room of this run's earlier photos, to add this photo to (missing for the
    /// run's first photo, which makes the room).
    /// </param>
    /// <param name="note">The optional <c>note</c> form field: what the photo is, such as its view's name.</param>
    /// <param name="generate">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the room (waiting for its analysis), 400 when no photo could be made, or 404.</returns>
    private static async Task<Results<Ok<RoomResponse>, ProblemHttpResult, NotFound>> GenerateAsync(
        Guid id, IFormFile guide, [FromForm] string? style, [FromForm] string? instructions, [FromForm] int? seed, [FromForm] string? boxes,
        [FromForm] int? view, [FromForm] string? corner, [FromForm] Guid? roomId, [FromForm] string? note, GeneratePlanRoom generate,
        CancellationToken cancellationToken)
    {
        var photoStyle = PhotoStyle.AsBuilt;
        List<ItemBox>? layout = null;
        PlanShot? shot = null;

        if (!string.IsNullOrWhiteSpace(style) && (!Enum.TryParse(style, ignoreCase: true, out photoStyle) || !Enum.IsDefined(photoStyle)))
        {
            return Problems.BadRequest(Application.Constants.Messages.UnknownPhotoStyle, Constants.Titles.GenerateFailed);
        }

        if (guide.Length > Application.Constants.Builder.MaxGuideBytes)
        {
            return Problems.BadRequest(Application.Constants.Messages.GuideImage, Constants.Titles.GenerateFailed);
        }

        if (!string.IsNullOrWhiteSpace(boxes))
        {
            try
            {
                var read = JsonSerializer.Deserialize<List<ItemBoxRequest>>(boxes, Json);

                layout = read?.Select(b => new ItemBox(b.Item, b.Left, b.Top, b.Right, b.Bottom)).ToList();
            }
            catch (JsonException)
            {
                return Problems.BadRequest(Application.Constants.Messages.PhotoBoxes, Constants.Titles.GenerateFailed);
            }
        }

        if (view is { } wall)
        {
            var spot = ViewCorner.None;

            if (!string.IsNullOrWhiteSpace(corner) && (!Enum.TryParse(corner, ignoreCase: true, out spot) || !Enum.IsDefined(spot)))
            {
                return Problems.BadRequest(Application.Constants.Messages.PhotoShot, Constants.Titles.GenerateFailed);
            }

            shot = new PlanShot(wall, spot);
        }

        using var buffer = new MemoryStream();

        await using (var content = guide.OpenReadStream())
            await content.CopyToAsync(buffer, cancellationToken);

        var outcome = await generate.HandleAsync(id, buffer.ToArray(), photoStyle, instructions, seed, layout, shot, roomId, note, cancellationToken);

        if (outcome is null)
        {
            return TypedResults.NotFound();
        }

        return outcome.Succeeded
            ? TypedResults.Ok(RoomResponse.From(outcome.Value))
            : Problems.BadRequest(outcome.Error, Constants.Titles.GenerateFailed);
    }

    /// <summary>An object of a request as a domain object.</summary>
    /// <param name="item">The object as sent.</param>
    /// <exception cref="DomainException">Its name, size, position, shape, turn, outline, decorations or color is invalid.</exception>
    private static PlanItem ToItem(PlanItemRequest item)
    {
        return new PlanItem(item.Label ?? "", item.Shape, item.X, item.Y, item.Width, item.Depth, item.Height, item.Rotation, HexColor.Parse(item.Color ?? ""),
            item.Wall, item.Elevation, item.Form, item.Parts?.ToDomain(), item.Decorations is null ? null : new ItemDecorations(item.Decorations), item.Material);
    }
}
