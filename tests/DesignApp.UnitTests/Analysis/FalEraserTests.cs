using System.Net;
using System.Text;
using DesignApp.Infrastructure.Ai;
using DesignApp.Infrastructure.Ai.Fal;
using DesignApp.Infrastructure.Photos;
using SkiaSharp;
using Microsoft.Extensions.Logging.Abstractions;
using static DesignApp.Infrastructure.Constants;

namespace DesignApp.UnitTests.Analysis;

/// <summary>
/// fal.ai's eraser through the queue: one paid submit, status checks until it is done, then the result; a failed
/// job or a broken status check never sends the job again and ends in the user's message.
/// </summary>
public class FalEraserTests
{
    private static readonly byte[] Photo = [1, 2, 3];
    private static readonly byte[] Mask = [4, 5, 6];
    private static readonly byte[] Result = [7, 8, 9];

    [Fact]
    public async Task A_queued_job_is_submitted_once_followed_and_its_result_fetched()
    {
        var fal = new StubQueue(["IN_QUEUE", "IN_PROGRESS", "COMPLETED"]);
        var eraser = Eraser(fal, RemovalQuality.Medium);

        var removed = await eraser.RemoveAsync(Photo, Mask, brushed: false, CancellationToken.None);

        Assert.True(removed.Succeeded, removed.Error);
        Assert.Equal(Result, removed.Value);
        Assert.Equal(1, fal.Submits);
        Assert.Equal(3, fal.StatusChecks);
        Assert.Contains("\"model\":\"medium_quality\"", fal.SubmitBody, StringComparison.Ordinal);
        Assert.Contains("\"mask_url\":\"data:image/png;base64,BAUG\"", fal.SubmitBody, StringComparison.Ordinal);
        Assert.Contains("\"mask_type\":\"automatic\"", fal.SubmitBody, StringComparison.Ordinal); // outlined by the AI
    }

    [Fact]
    public async Task Erasers_that_answer_with_one_image_work_too()
    {
        var fal = new StubQueue(["COMPLETED"], singleImage: true);

        var removed = await Eraser(fal, handMaskGrowth: 0).RemoveAsync(Photo, Mask, brushed: true, CancellationToken.None);

        Assert.Equal(Result, removed.Value);
        Assert.Contains("\"mask_type\":\"manual\"", fal.SubmitBody, StringComparison.Ordinal); // marked by hand
    }

    [Fact]
    public void A_hand_marked_mask_grows_in_every_direction_and_nothing_else_changes()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(40, 40, SKColorType.Gray8, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Black);
            canvas.DrawRect(18, 18, 4, 4, new SKPaint { Color = SKColors.White }); // pixels 18..21
        }
        var png = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100).ToArray();

        using var grown = SKBitmap.Decode(MaskGrowth.Grow(png, 5));

        Assert.Equal(255, grown.GetPixel(13, 20).Red);  // 5 to the left of the square
        Assert.Equal(255, grown.GetPixel(26, 26).Red);  // 5 down and right (a square reach)
        Assert.Equal(0, grown.GetPixel(12, 20).Red);    // 6 away: untouched
        Assert.Equal(0, grown.GetPixel(20, 27).Red);
        Assert.Same(png, MaskGrowth.Grow(png, 0));
    }

    [Fact]
    public async Task A_failed_job_is_not_sent_again_and_the_user_gets_a_message()
    {
        var fal = new StubQueue(["IN_PROGRESS", "COMPLETED"], error: "model crashed");

        var removed = await Eraser(fal).RemoveAsync(Photo, Mask, brushed: false, CancellationToken.None);

        Assert.False(removed.Succeeded);
        Assert.Equal(DesignApp.Infrastructure.Constants.Ai.Removal.Messages.Failed, removed.Error);
        Assert.Equal(1, fal.Submits);
        Assert.Equal(0, fal.ResultFetches);
    }

    [Fact]
    public async Task Broken_status_checks_end_in_the_users_message_without_a_second_submit()
    {
        var fal = new StubQueue([], statusFails: true);

        var removed = await Eraser(fal).RemoveAsync(Photo, Mask, brushed: false, CancellationToken.None);

        Assert.Equal(DesignApp.Infrastructure.Constants.Ai.Removal.Messages.Failed, removed.Error);
        Assert.Equal((1, DesignApp.Infrastructure.Constants.Ai.Removal.MaxStatusErrors), (fal.Submits, fal.StatusChecks));
    }

    /// <summary>The eraser on a stubbed fal.ai.</summary>
    /// <param name="fal">The stub.</param>
    /// <param name="quality">The quality tier in the settings.</param>
    /// <param name="handMaskGrowth">How far hand-marked masks are grown (0 keeps the stub's mask bytes as they are).</param>
    private static FalObjectRemovalProvider Eraser(StubQueue fal, RemovalQuality quality = RemovalQuality.Best, int handMaskGrowth = 12)
    {
        return new FalObjectRemovalProvider(new HttpClient(fal),
            new FixedAiSettings(new AiSettings { RemovalQuality = quality, RemovalHandMaskGrowth = handMaskGrowth }),
            NullLogger<FalObjectRemovalProvider>.Instance);
    }

    /// <summary>fal.ai's queue: a submit gives a ticket, status checks walk through the given states, the result is an image.</summary>
    /// <param name="states">The status each check returns, in order (the last one repeats).</param>
    /// <param name="error">An error reported with COMPLETED.</param>
    /// <param name="statusFails">Answers every status check with a server error.</param>
    /// <param name="singleImage">Answers with one image, as Bria's and Ideogram's erasers do, instead of a list.</param>
    private sealed class StubQueue(string[] states, string? error = null, bool statusFails = false, bool singleImage = false) : HttpMessageHandler
    {
        /// <summary>How many jobs were submitted (each one paid).</summary>
        public int Submits { get; private set; }
        /// <summary>How many status checks were made.</summary>
        public int StatusChecks { get; private set; }
        /// <summary>How many results were fetched.</summary>
        public int ResultFetches { get; private set; }
        /// <summary>The submitted job.</summary>
        public string SubmitBody { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post)
            {
                Submits++;
                SubmitBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                return Json("""{"request_id":"r1","status_url":"https://queue.fal.run/fal-ai/object-removal/requests/r1/status","response_url":"https://queue.fal.run/fal-ai/object-removal/requests/r1"}""");
            }
            if (path.EndsWith("/status", StringComparison.Ordinal))
            {
                var state = states.Length == 0 ? "IN_QUEUE" : states[Math.Min(StatusChecks, states.Length - 1)];
                StatusChecks++;
                if (statusFails)
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                return Json(error is not null && state == "COMPLETED"
                    ? $$"""{"status":"COMPLETED","error":"{{error}}"}"""
                    : $$"""{"status":"{{state}}"}""");
            }
            ResultFetches++;
            return Json(singleImage
                ? $$$"""{"image":{"url":"data:image/png;base64,{{{Convert.ToBase64String(Result)}}}"}}"""
                : $$"""{"images":[{"url":"data:image/jpeg;base64,{{Convert.ToBase64String(Result)}}"}]}""");
        }

        /// <summary>A JSON answer.</summary>
        /// <param name="body">The JSON.</param>
        private static HttpResponseMessage Json(string body)
        {
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
