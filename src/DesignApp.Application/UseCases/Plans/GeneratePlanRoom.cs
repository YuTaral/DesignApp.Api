using DesignApp.Application.Helpers;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;
using Microsoft.Extensions.Logging;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.UseCases;

/// <summary>
/// Turns a built room into a realistic photo: the prompt is written from the plan with its exact sizes and positions
/// (<see cref="PlanPrompt"/>), and the image model turns the room's simple 3D view into a photo with that layout. The
/// photos of one Generate run are one room: the first makes the room (its photo, the room's), the others are added to
/// it to choose from. Its analysis waits until the user starts it (with the real AI it costs money). A signed-in user's
/// room goes into the plan's own design.
/// </summary>
/// <param name="plans">Where plans are stored.</param>
/// <param name="rooms">Where rooms and their photos are stored.</param>
/// <param name="currentUser">The signed-in user or guest.</param>
/// <param name="ai">The AI providers.</param>
/// <param name="takes">Stores the photos.</param>
/// <param name="planDesigns">Puts the room into the plan's design.</param>
/// <param name="clock">When the room is made.</param>
/// <param name="logger">Records a room that couldn't be put into the plan's design.</param>
public sealed partial class GeneratePlanRoom(
    IPlanRepository plans, IRoomRepository rooms, ICurrentUser currentUser, IAiProviders ai, TakeStore takes, PlanDesigns planDesigns, TimeProvider clock,
    ILogger<GeneratePlanRoom> logger)
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
    /// <param name="roomId">The room of this run's earlier photos, to add this one to; <c>null</c> for the run's first.</param>
    /// <param name="note">What the photo is, such as its view's name.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The room (waiting for its analysis), a failure the user can read, or <c>null</c> when the user has no such plan.</returns>
    public async Task<Result<Room>?> HandleAsync(
        Guid planId, byte[] guidePng, PhotoStyle style, string? instructions, int? seed, IReadOnlyList<ItemBox>? boxes, PlanShot? shot,
        Guid? roomId, string? note, CancellationToken cancellationToken)
    {
        var plan = await plans.FindAsync(planId, currentUser.Id, cancellationToken);

        if (plan is null)
        {
            return null;
        }

        var room = roomId is Guid id ? await rooms.FindAsync(id, currentUser.Id, cancellationToken) : null;

        if (roomId is not null && room?.PlanId != plan.Id)
        {
            return Result.Failure<Room>(Constants.Messages.NotThisPlansRoom);
        }

        if ((note?.Trim().Length ?? 0) > Domain.Constants.Rooms.MaxTakeNoteLength)
        {
            return Result.Failure<Room>(Constants.Messages.PhotoNote);
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

        var newId = room?.Id ?? Guid.CreateVersion7(clock.GetUtcNow());
        var stored = await takes.StoreAsync(newId, photo.Value, note ?? "", null, cancellationToken);

        if (!stored.Succeeded)
        {
            return Result.Failure<Room>(stored.Error);
        }

        if (room is null)
        {
            await takes.UseAsRoomPhotoAsync(stored.Value, cancellationToken);
            room = Room.FromPlan(newId, currentUser.Id, Constants.Rooms.PhotoKey(newId), stored.Value, clock.GetUtcNow(), plan.AiMode, plan.Id);
            rooms.Add(room);
        }

        rooms.AddTake(stored.Value);
        await rooms.SaveChangesAsync(cancellationToken);

        // The photo is made and saved, so the design can't lose it: when the room can't go into the plan's design now
        // (it is full, or another run of the same plan made the design at the same moment), the photo page puts it there.
        try
        {
            await planDesigns.AddAsync(plan, room, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            LogNotInDesign(error, room.Id, plan.Id);
        }

        return room;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Room {RoomId} of plan {PlanId} couldn't be put into the plan's design; the photo page puts it there")]
    private partial void LogNotInDesign(Exception error, Guid roomId, Guid planId);

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
