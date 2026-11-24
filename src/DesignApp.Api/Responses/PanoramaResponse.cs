namespace DesignApp.Api.Responses;

/// <summary>A room's 360-degree photo as the API returns it: where to load it, and its size.</summary>
/// <param name="Url">Where to load it, relative to the API's base URL.</param>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels (half its width).</param>
/// <param name="StartYaw">Which way its views start, in degrees from its middle: right is positive.</param>
public sealed record PanoramaResponse(string Url, int Width, int Height, double StartYaw);
