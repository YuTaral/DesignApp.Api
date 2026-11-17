using DesignApp.Api.Helpers;
using DesignApp.Application.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace DesignApp.Api.Endpoints;

/// <summary>The <c>/api/dev</c> endpoints: tools for building the app, mapped in Development only.</summary>
internal static class DevEndpoints
{
    /// <summary>Maps the development endpoints under <c>/dev</c>.</summary>
    /// <param name="api">The <c>/api</c> route group.</param>
    /// <returns>The same group, for chaining.</returns>
    public static RouteGroupBuilder MapDevEndpoints(this RouteGroupBuilder api)
    {
        var dev = api.MapGroup(Constants.Routes.Dev).WithTags(Constants.OpenApi.DevTag);

        dev.MapGet(Constants.Routes.LibraryExport, ExportAsync)
            .WithName("ExportLibraryRoom")
            .WithSummary("Packs one of your rooms, as it is now, as the files of a library layout (a zip).")
            .WithDescription("`name` is the layout's id, such as `2`. Unzip the files into the room's folder " +
                "under `src/DesignApp.Infrastructure/Library/<design>/` and list the layout in the design's `design.json`. " +
                "Objects removed from the photo are left out.");

        return api;
    }

    /// <summary><c>GET /api/dev/library-export/{id}?name=</c>.</summary>
    /// <param name="id">The room's id.</param>
    /// <param name="name">The layout id the files are named after.</param>
    /// <param name="export">The use case.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>200 with the zip, 400 for an unusable name, or 404 when the room is missing or not Ready.</returns>
    private static async Task<Results<FileContentHttpResult, ProblemHttpResult, NotFound>> ExportAsync(
        Guid id, [FromQuery] string? name, ExportLibraryRoom export, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(name) || !name.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
        {
            return Problems.BadRequest(Constants.Messages.ExportName, Constants.Titles.InvalidExport);
        }

        var zip = await export.HandleAsync(id, name, cancellationToken);

        if (zip is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.File(zip, Constants.Http.Zip, $"{name}.zip");
    }
}
