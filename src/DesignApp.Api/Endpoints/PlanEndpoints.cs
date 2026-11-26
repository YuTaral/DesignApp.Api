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
        plans.MapGet(Constants.Routes.Root, ListAsync)
            .WithName("ListPlans")
            .WithSummary("Lists the rooms the user builds, the most recently saved first, with how many objects and photos each has.");
        plans.MapGet(Constants.Routes.ById, GetAsync)
            .WithName("GetPlan")
            .WithSummary("Returns a built room with its walls and objects.");
        plans.MapDelete(Constants.Routes.ById, DeleteAsync)
            .WithName("DeletePlan")
            .WithSummary("Deletes a built room. The rooms holding its photos stay, in their designs, without its 3D view.");
        plans.MapPut(Constants.Routes.ById, SaveAsync)
            .WithName("SavePlan")
            .WithSummary("Saves a built room as the user left it: name, walls, colors and objects (positions in centimetres from the back-left corner).");
        plans.MapPost(Constants.Routes.PlanDescribe, DescribeAsync)
            .WithName("DescribePlanObject")
            .WithSummary("Turns a description of an object into one that can be placed, with its size and color (the AI; Claude Haiku when real).")
            .WithDescription("The object isn't added to the room: place it, then save the room. With the real AI this is a paid call, so it is rate-limited.")
            .RequireRateLimiting(Constants.RateLimits.RemovalsPolicy);
        plans.MapPost(Constants.Routes.PlanPanorama, GeneratePanoramaAsync)
            .WithName("GeneratePlanPanorama")
            .WithSummary("Makes one 360-degree photo of the built room from its 360-degree 3D view (multipart field `guide`, a PNG twice as wide as it is tall) and returns the new room made from it.")
            .WithDescription("Form fields: `spotX` and `spotY` (where the camera stands, in cm on the plan), and optionally `startYaw` (which way its views start), `boxes` (each object's box in the 3D view), " +
                "`style`, `instructions` and `replaces` (an older room of the plan the new room replaces). The room's photo is the view straight ahead, cut from " +
                "the 360-degree photo; more views are saved from it without AI. With the real AI this is paid (a 4K photo), so it is rate-limited.")
            .DisableAntiforgery() // the cookies are SameSite=Lax, so another site's form posts arrive without them
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

    /// <summary><c>GET /api/plans</c>: the rooms the user builds.</summary>
    /// <param name="getPlans">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the plans, the most recently saved first.</returns>
    private static async Task<Ok<List<PlanSummaryResponse>>> ListAsync(GetPlans getPlans, CancellationToken cancellationToken)
    {
        var summaries = await getPlans.HandleAsync(cancellationToken);

        return TypedResults.Ok(summaries.Select(PlanSummaryResponse.From).ToList());
    }

    /// <summary><c>DELETE /api/plans/{id}</c>: deletes a built room; its photos' rooms stay.</summary>
    /// <param name="id">The plan's id.</param>
    /// <param name="deletePlan">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>204, or 404 when the user has no such plan.</returns>
    private static async Task<Results<NoContent, NotFound>> DeleteAsync(Guid id, DeletePlan deletePlan, CancellationToken cancellationToken)
    {
        var deleted = await deletePlan.HandleAsync(id, cancellationToken);

        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
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
    /// <param name="masks">
    /// The optional <c>masks</c> file: which object or surface each pixel of the 3D view shows (PNG, the id in its red
    /// channel), to bring the photo's colors back to the room as planned.
    /// </param>
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
    /// <param name="replaces">
    /// The optional <c>replaces</c> form field, with the run's first photo: an older room of the plan which the new room
    /// replaces in its designs (the old room is deleted).
    /// </param>
    /// <param name="referenceBoxes">
    /// The optional <c>referenceBoxes</c> form field, with the run's later photos: a JSON list of each object's box in the
    /// run's first photo (<see cref="ItemBoxRequest"/>), from which those objects are copied.
    /// </param>
    /// <param name="generate">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the room (waiting for its analysis), 400 when no photo could be made, or 404.</returns>
    private static async Task<Results<Ok<RoomResponse>, ProblemHttpResult, NotFound>> GenerateAsync(
        Guid id, IFormFile guide, IFormFile? masks, [FromForm] string? style, [FromForm] string? instructions, [FromForm] int? seed, [FromForm] string? boxes,
        [FromForm] int? view, [FromForm] string? corner, [FromForm] Guid? roomId, [FromForm] string? note, [FromForm] Guid? replaces, [FromForm] string? referenceBoxes,
        GeneratePlanRoom generate,
        CancellationToken cancellationToken)
    {
        PlanShot? shot = null;

        if (!TryReadStyle(style, out var photoStyle))
        {
            return Problems.BadRequest(Application.Constants.Messages.UnknownPhotoStyle, Constants.Titles.GenerateFailed);
        }

        if (guide.Length > Application.Constants.Builder.MaxGuideBytes)
        {
            return Problems.BadRequest(Application.Constants.Messages.GuideImage, Constants.Titles.GenerateFailed);
        }

        if (!TryReadBoxes(boxes, out var layout) || !TryReadBoxes(referenceBoxes, out var inReference))
        {
            return Problems.BadRequest(Application.Constants.Messages.PhotoBoxes, Constants.Titles.GenerateFailed);
        }

        if (masks?.Length > Application.Constants.Builder.MaxMasksBytes)
        {
            return Problems.BadRequest(Application.Constants.Messages.PhotoMasks, Constants.Titles.GenerateFailed);
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

        var bytes = await ReadAllAsync(guide, cancellationToken);
        var masksBytes = masks is null ? null : await ReadAllAsync(masks, cancellationToken);
        var run = new PhotoRun(roomId, note, replaces, inReference);
        var outcome = await generate.HandleAsync(id, bytes, masksBytes, photoStyle, instructions, seed, layout, shot, run, cancellationToken);

        return ToRoomResult(outcome);
    }

    /// <summary><c>POST /api/plans/{id}/panorama</c>: a 360-degree photo of the built room, as a new room.</summary>
    /// <param name="id">The plan's id.</param>
    /// <param name="guide">The room's 360-degree 3D view (PNG).</param>
    /// <param name="spotX">The <c>spotX</c> form field: where the camera stands, across the plan in cm.</param>
    /// <param name="spotY">The <c>spotY</c> form field: where the camera stands, down the plan in cm.</param>
    /// <param name="startYaw">The optional <c>startYaw</c> form field: which way its views start, in degrees from its middle (0 when missing).</param>
    /// <param name="style">The optional <c>style</c> form field: the photo's interior style.</param>
    /// <param name="instructions">The optional <c>instructions</c> form field: the user's own words for the AI.</param>
    /// <param name="boxes">The optional <c>boxes</c> form field: a JSON list of each object's box in the 3D view (<see cref="ItemBoxRequest"/>).</param>
    /// <param name="replaces">The optional <c>replaces</c> form field: an older room of the plan which the new room replaces.</param>
    /// <param name="generate">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the room, 400 when no photo could be made, or 404.</returns>
    private static async Task<Results<Ok<RoomResponse>, ProblemHttpResult, NotFound>> GeneratePanoramaAsync(
        Guid id, IFormFile guide, [FromForm] double spotX, [FromForm] double spotY, [FromForm] double? startYaw, [FromForm] string? style, [FromForm] string? instructions,
        [FromForm] string? boxes, [FromForm] Guid? replaces, GeneratePlanPanorama generate, CancellationToken cancellationToken)
    {
        var spot = new OutlinePoint(spotX, spotY);

        if (!TryReadStyle(style, out var photoStyle))
        {
            return Problems.BadRequest(Application.Constants.Messages.UnknownPhotoStyle, Constants.Titles.GenerateFailed);
        }

        if (guide.Length > Application.Constants.Panoramas.MaxGuideBytes)
        {
            return Problems.BadRequest(Application.Constants.Messages.PanoramaGuide, Constants.Titles.GenerateFailed);
        }

        if (!TryReadBoxes(boxes, out var layout))
        {
            return Problems.BadRequest(Application.Constants.Messages.PhotoBoxes, Constants.Titles.GenerateFailed);
        }

        var bytes = await ReadAllAsync(guide, cancellationToken);
        var outcome = await generate.HandleAsync(id, bytes, photoStyle, instructions, layout, spot, startYaw ?? 0, replaces, cancellationToken);

        return ToRoomResult(outcome);
    }

    /// <summary>Reads the optional <c>style</c> form field.</summary>
    /// <param name="style">The field; empty or missing for the room as planned.</param>
    /// <param name="photoStyle">The style.</param>
    /// <returns>Whether the field was empty or a known style.</returns>
    private static bool TryReadStyle(string? style, out PhotoStyle photoStyle)
    {
        photoStyle = PhotoStyle.AsBuilt;

        return string.IsNullOrWhiteSpace(style) || (Enum.TryParse(style, ignoreCase: true, out photoStyle) && Enum.IsDefined(photoStyle));
    }

    /// <summary>An uploaded file's bytes.</summary>
    /// <param name="file">The file.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    private static async Task<byte[]> ReadAllAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();

        await using (var content = file.OpenReadStream())
            await content.CopyToAsync(buffer, cancellationToken);

        return buffer.ToArray();
    }

    /// <summary>A photo use case's outcome as the endpoint's result.</summary>
    /// <param name="outcome">The new room, a failure the user can read, or <c>null</c> when the user has no such plan.</param>
    /// <returns>200 with the room, 400 with why no photo could be made, or 404.</returns>
    private static Results<Ok<RoomResponse>, ProblemHttpResult, NotFound> ToRoomResult(Result<Room>? outcome)
    {
        if (outcome is null)
        {
            return TypedResults.NotFound();
        }

        return outcome.Succeeded
            ? TypedResults.Ok(RoomResponse.From(outcome.Value))
            : Problems.BadRequest(outcome.Error, Constants.Titles.GenerateFailed);
    }

    /// <summary>Reads a form field's JSON list of object boxes.</summary>
    /// <param name="json">The field; empty or missing for none.</param>
    /// <param name="boxes">The boxes, or <c>null</c> for none.</param>
    /// <returns>Whether the field was empty or a list of boxes.</returns>
    private static bool TryReadBoxes(string? json, out List<ItemBox>? boxes)
    {
        boxes = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            var read = JsonSerializer.Deserialize<List<ItemBoxRequest>>(json, Json);

            boxes = read?.Select(b => new ItemBox(b.Item, b.Left, b.Top, b.Right, b.Bottom)).ToList();

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>An object of a request as a domain object.</summary>
    /// <param name="item">The object as sent.</param>
    /// <exception cref="DomainException">Its name, size, position, shape, turn, outline, decorations or color is invalid.</exception>
    private static PlanItem ToItem(PlanItemRequest item)
    {
        return new PlanItem(item.Label ?? "", item.Shape, item.X, item.Y, item.Width, item.Depth, item.Height, item.Rotation, HexColor.Parse(item.Color ?? ""),
            item.Wall, item.Elevation, item.Form, item.Parts?.ToDomain(), item.Decorations is null ? null : new ItemDecorations(item.Decorations), item.Material,
            item.Covering?.ToDomain(), item.Worktop?.ToDomain());
    }
}
