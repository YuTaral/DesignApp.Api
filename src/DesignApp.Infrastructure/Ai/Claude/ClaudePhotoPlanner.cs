using System.Diagnostics;
using Anthropic;
using Anthropic.Models.Messages;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using Microsoft.Extensions.Logging;

using P = DesignApp.Infrastructure.Constants.Ai.RoomBuilder.Photo;
using RB = DesignApp.Infrastructure.Constants.Ai.RoomBuilder;

namespace DesignApp.Infrastructure.Ai.Claude;

/// <summary>
/// Reads a room photo into a plan on Claude (vision with a structured answer): the instructions, the catalog to pick
/// from and the answer's schema come from <see cref="PhotoPlanJson"/>.
/// </summary>
/// <param name="client">The Anthropic client.</param>
/// <param name="logger">Records the tokens each call used.</param>
internal sealed partial class ClaudePhotoPlanner(IAnthropicClient client, ILogger<ClaudePhotoPlanner> logger) : IPhotoPlanner
{
    /// <inheritdoc/>
    public async Task<Result<PhotoPlan>> PlanFromPhotoAsync(AnalysisImage image, IReadOnlyList<CatalogObject> catalog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        var started = Stopwatch.GetTimestamp();
        var photo = new ImageBlockParam { Source = new Base64ImageSource { Data = Convert.ToBase64String(image.Jpeg), MediaType = MediaType.ImageJpeg } };
        var text = new TextBlockParam { Text = PhotoPlanJson.Message(catalog, image.Width, image.Height) };
        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model = P.Model,
            MaxTokens = P.MaxTokens,
            System = P.System,
            OutputConfig = new OutputConfig { Effort = Effort.Medium, Format = new JsonOutputFormat { Schema = PhotoPlanJson.Schema(catalog) } },
            Messages = [new UserMessageParam { Content = new List<ContentBlockParam> { photo, text } }],
        }, cancellationToken);
        var seconds = Stopwatch.GetElapsedTime(started).TotalSeconds;

        LogUsage(P.Model, response.Usage.InputTokens, response.Usage.OutputTokens, seconds);

        return response.StopReason == StopReason.EndTurn ? PhotoPlanJson.Parse(ClaudeAnswer.Text(response)) : Result.Failure<PhotoPlan>(RB.Messages.PhotoPlanFailed);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Claude {Model} read a photo into a plan with {InputTokens} input and {OutputTokens} output tokens in {Seconds:0.0} s")]
    private partial void LogUsage(string model, long inputTokens, long outputTokens, double seconds);
}
