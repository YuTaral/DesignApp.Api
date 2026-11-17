using DesignApp.Application.Helpers;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.UseCases;

/// <summary>
/// Turns a built room into a realistic photo and makes it an ordinary room: the prompt is written from the plan with
/// its exact sizes and positions (<see cref="PlanPrompt"/>), the image model turns the room's simple 3D view into a photo
/// with that layout, and the photo is
/// uploaded like any other. Its analysis waits until the user starts it in the editor (with the real AI it costs money),
/// and then the user can restyle it.
/// </summary>
/// <param name="plans">Where plans are stored.</param>
/// <param name="currentUser">The signed-in user or guest.</param>
/// <param name="ai">The AI providers.</param>
/// <param name="uploadRoom">Makes a room from the photo.</param>
public sealed class GeneratePlanRoom(IPlanRepository plans, ICurrentUser currentUser, IAiProviders ai, UploadRoom uploadRoom)
{
    /// <summary>Generates the photo and the room.</summary>
    /// <param name="planId">The plan.</param>
    /// <param name="guidePng">The room's simple 3D view as the browser drew it (PNG).</param>
    /// <param name="style">The interior style of the photo.</param>
    /// <param name="instructions">The user's own extra instructions for the AI, if any.</param>
    /// <param name="seed">Makes the photo repeatable (see <see cref="IImageGenerator.GenerateAsync"/>); <c>null</c> for a random one.</param>
    /// <param name="boxes">
    /// Where each object is in the 3D view, as the browser drew it; the image model must keep each one in its box.
    /// <c>null</c> to send none.
    /// </param>
    /// <param name="shot">
    /// The view the photo is made from (the guide must be drawn from it); <c>null</c> for the room's own. The plan keeps
    /// its own view either way.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The new room (waiting for its analysis), a failure the user can read, or <c>null</c> when the user has no such plan.</returns>
    public async Task<Result<Room>?> HandleAsync(Guid planId, byte[] guidePng, PhotoStyle style, string? instructions, int? seed, IReadOnlyList<ItemBox>? boxes, PlanShot? shot, CancellationToken cancellationToken)
    {
        var plan = await plans.FindAsync(planId, currentUser.Id, cancellationToken);

        if (plan is null)
        {
            return null;
        }

        if (guidePng.Length is 0 or > Constants.Builder.MaxGuideBytes)
        {
            return Result.Failure<Room>(Constants.Messages.GuideImage);
        }

        if (plan.Items.Count == 0)
        {
            return Result.Failure<Room>(Constants.Messages.EmptyPlan);
        }

        if (!Enum.IsDefined(style))
        {
            return Result.Failure<Room>(Constants.Messages.UnknownPhotoStyle);
        }

        if ((instructions?.Trim().Length ?? 0) > Constants.Builder.MaxInstructionsLength)
        {
            return Result.Failure<Room>(Constants.Messages.PhotoInstructions);
        }

        if (seed < 0)
        {
            return Result.Failure<Room>(Constants.Messages.PhotoSeed);
        }

        if (boxes is not null && (boxes.Count > Domain.Constants.Plans.MaxItems || boxes.Any(box => !Fits(box, plan.Items.Count))))
        {
            return Result.Failure<Room>(Constants.Messages.PhotoBoxes);
        }

        if (shot is not null && (shot.View < 0 || shot.View >= plan.Outline.Walls.Count || !Enum.IsDefined(shot.Corner)))
        {
            return Result.Failure<Room>(Constants.Messages.PhotoShot);
        }

        var prompt = PlanPrompt.Write(plan, style, instructions, shot);
        var photo = await ai.Images(plan.AiMode).GenerateAsync(guidePng, prompt, seed, boxes?.Select(box => Layout(plan, box)).ToList(), cancellationToken);

        if (!photo.Succeeded)
        {
            return Result.Failure<Room>(photo.Error);
        }

        using var content = new MemoryStream(photo.Value, writable: false);

        return await uploadRoom.HandleAsync(content, cancellationToken, AnalysisScope.WholeRoom, plan.AiMode, analyze: false);
    }

    /// <summary>Whether a box is on the plan's objects and inside the picture.</summary>
    /// <param name="box">The box.</param>
    /// <param name="items">How many objects the plan has.</param>
    private static bool Fits(ItemBox box, int items)
    {
        var edges = new[] { box.Left, box.Top, box.Right, box.Bottom };

        return box.Item >= 0 && box.Item < items && edges.All(e => e is >= 0 and <= 1) && box.Left < box.Right && box.Top < box.Bottom;
    }

    /// <summary>A box as the image model takes it: an id, the object in words, and the box.</summary>
    /// <param name="plan">The room.</param>
    /// <param name="box">One object's box.</param>
    private static LayoutBox Layout(RoomPlan plan, ItemBox box)
    {
        var item = plan.Items[box.Item];

        return new LayoutBox($"{Constants.Builder.BoxLabel}{box.Item + 1}", PlanPrompt.Describe(item), box.Left, box.Top, box.Right, box.Bottom);
    }
}
