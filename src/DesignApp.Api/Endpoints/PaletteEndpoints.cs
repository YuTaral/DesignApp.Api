using DesignApp.Api.Responses;
using DesignApp.Application.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DesignApp.Api.Endpoints;

/// <summary>The <c>/api/palettes</c> endpoint: the real paint colors the editor offers, such as RAL Classic.</summary>
internal static class PaletteEndpoints
{
    /// <summary>Maps the palette endpoint under <c>/palettes</c>.</summary>
    /// <param name="api">The <c>/api</c> route group.</param>
    /// <returns>The same group, for chaining.</returns>
    public static RouteGroupBuilder MapPaletteEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet(Constants.Routes.Palettes, List)
            .WithTags(Constants.OpenApi.PalettesTag)
            .WithName("ListPalettes")
            .WithSummary("Lists the paint palettes with their paints: code, name and on-screen color.")
            .WithDescription("A recolor may name one of these paints (`paint`); its color must then be the paint's color.");

        return api;
    }

    /// <summary><c>GET /api/palettes</c>.</summary>
    /// <param name="palettes">The palettes.</param>
    /// <param name="response">Used to set the caching headers.</param>
    private static Ok<List<PaletteResponse>> List(IPaletteCatalog palettes, HttpResponse response)
    {
        response.Headers.CacheControl = Constants.Http.LibraryCache;

        return TypedResults.Ok(palettes.Palettes.Select(PaletteResponse.From).ToList());
    }
}
