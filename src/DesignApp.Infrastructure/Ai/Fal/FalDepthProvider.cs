using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Infrastructure.Photos;
using Microsoft.Extensions.Logging;
using Polly.Timeout;
using SkiaSharp;

namespace DesignApp.Infrastructure.Ai.Fal;

/// <summary>
/// Depth from MoGe-2 on fal.ai: the photo, scaled down to the cheapest size, goes out once (each call is paid, so it
/// is never repeated); the point cloud that comes back is put into the depth grid through the camera matrix MoGe-2
/// estimated, and cells it left out (sky, uncertain edges) are filled from their neighbors.
/// </summary>
/// <param name="http">An HTTP client with the fal.ai key and a timeout, without retries.</param>
/// <param name="logger">Records each measurement, so its cost can be counted, and why one failed.</param>
internal sealed partial class FalDepthProvider(HttpClient http, ILogger<FalDepthProvider> logger) : IDepthProvider
{
    /// <inheritdoc/>
    public async Task<Result<DepthMeasurement>> MeasureAsync(DepthInput input, CancellationToken cancellationToken)
    {
        try
        {
            var request = new DepthRequest(
                Constants.Ai.Fal.JpegDataUri + Convert.ToBase64String(ScaleDown(input.Photo)),
                Constants.Ai.Depth.Model, ExportGlb: false, ExportPly: true, ApplyMask: true);
            using var response = await http.PostAsJsonAsync(new Uri(Constants.Ai.Depth.Endpoint), request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var answer = await response.Content.ReadFromJsonAsync<DepthResponse>(cancellationToken);
            if (answer?.PointCloud?.Url is not { Length: > 0 } cloudUrl || answer.Intrinsics is not [[var fx, _, var cx], [_, var fy, var cy], ..]
                || answer.FovX is not > 0 || answer.FovY is not > 0)
            {
                return Failed("the answer has no point cloud, camera matrix or field of view");
            }

            var points = PlyCloud.Read(await http.GetByteArrayAsync(new Uri(cloudUrl), cancellationToken));
            var (width, height) = (Constants.Ai.Depth.GridWidth, DepthGrid.HeightFor(input.Width, input.Height));
            var grid = DepthGrid.Rasterize(points, fx, fy, cx, cy, width, height);
            if (!DepthGrid.FillHoles(grid, width, height))
                return Failed("the point cloud covers too little of the photo");
            LogMeasured(points.Length);
            return new DepthMeasurement(DepthGrid.ToPng(grid, width, height), answer.FovX.Value, answer.FovY.Value);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or FormatException or TimeoutRejectedException
            || (e is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return Failed(e.Message);
        }
    }

    /// <summary>Scales a photo down to at most <see cref="Constants.Ai.Depth.MaxInputPixels"/>, as a JPEG.</summary>
    /// <param name="photoJpeg">The photo.</param>
    private static byte[] ScaleDown(byte[] photoJpeg)
    {
        using var photo = SKBitmap.Decode(photoJpeg) ?? throw new FormatException("The photo can't be decoded.");
        var scale = Math.Min(1, Math.Sqrt((double)Constants.Ai.Depth.MaxInputPixels / (photo.Width * photo.Height)));
        if (scale >= 1)
            return photoJpeg;
        var info = new SKImageInfo(Math.Max(1, (int)(photo.Width * scale)), Math.Max(1, (int)(photo.Height * scale)));
        using var small = photo.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
            ?? throw new FormatException("The photo can't be scaled.");
        using var image = SKImage.FromBitmap(small);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, Constants.Ai.Depth.InputQuality);
        return encoded.ToArray();
    }

    /// <summary>Logs why a measurement failed and returns the user's message.</summary>
    /// <param name="reason">Why it failed.</param>
    private Result<DepthMeasurement> Failed(string reason)
    {
        LogFailed(reason);
        return Result.Failure<DepthMeasurement>(Constants.Ai.Depth.Messages.Failed);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Measured depth with MoGe-2 ({Points} points; one paid call)")]
    private partial void LogMeasured(int points);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Depth measurement failed: {Reason}")]
    private partial void LogFailed(string reason);

    /// <summary>The depth model's request.</summary>
    /// <param name="ImageUrl">The photo, as a data URI.</param>
    /// <param name="Model">The checkpoint.</param>
    /// <param name="ExportGlb">Whether to export a mesh (no).</param>
    /// <param name="ExportPly">Whether to export the point cloud (yes).</param>
    /// <param name="ApplyMask">Whether to leave out sky and uncertain pixels (yes).</param>
    private sealed record DepthRequest(
        [property: JsonPropertyName(Constants.Ai.Depth.Json.ImageUrl)] string ImageUrl,
        [property: JsonPropertyName(Constants.Ai.Depth.Json.Model)] string Model,
        [property: JsonPropertyName(Constants.Ai.Depth.Json.ExportGlb)] bool ExportGlb,
        [property: JsonPropertyName(Constants.Ai.Depth.Json.ExportPly)] bool ExportPly,
        [property: JsonPropertyName(Constants.Ai.Depth.Json.ApplyMask)] bool ApplyMask);

    /// <summary>The depth model's answer (the fields used).</summary>
    /// <param name="PointCloud">The point cloud file.</param>
    /// <param name="Intrinsics">The camera matrix, normalized to the image's size.</param>
    /// <param name="FovX">The horizontal field of view, in degrees.</param>
    /// <param name="FovY">The vertical field of view, in degrees.</param>
    private sealed record DepthResponse(
        [property: JsonPropertyName(Constants.Ai.Depth.Json.PointCloud)] DepthFile? PointCloud,
        [property: JsonPropertyName(Constants.Ai.Depth.Json.Intrinsics)] double[][]? Intrinsics,
        [property: JsonPropertyName(Constants.Ai.Depth.Json.FovX)] double? FovX,
        [property: JsonPropertyName(Constants.Ai.Depth.Json.FovY)] double? FovY);

    /// <summary>A file in the answer.</summary>
    /// <param name="Url">Where it is.</param>
    private sealed record DepthFile([property: JsonPropertyName(Constants.Ai.Depth.Json.Url)] string? Url);
}
