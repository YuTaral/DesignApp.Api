using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;
using Microsoft.Extensions.Logging;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.Helpers;

/// <summary>
/// What making photos of a room plan shares, whether one view at a time or a 360-degree photo: the checks of the
/// request, the objects' boxes for the image model, the room a new one replaces, and putting the new room into the
/// plan's design (in the replaced room's place, deleting that room).
/// </summary>
/// <param name="rooms">Where rooms are stored.</param>
/// <param name="currentUser">The signed-in user or guest.</param>
/// <param name="planDesigns">Puts the room into the plan's design.</param>
/// <param name="remover">Deletes a room replaced.</param>
/// <param name="logger">Records a room that couldn't be put into the plan's design.</param>
public sealed partial class PlanPhotos(IRoomRepository rooms, ICurrentUser currentUser, PlanDesigns planDesigns, RoomRemover remover, ILogger<PlanPhotos> logger)
{
    /// <summary>Why a photo request can't be made, or <c>null</c> when it can.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="guide">The 3D view the browser drew.</param>
    /// <param name="maxBytes">The largest 3D view allowed.</param>
    /// <param name="style">The interior style.</param>
    /// <param name="instructions">The user's own extra instructions, if any.</param>
    public static string? Problem(RoomPlan plan, byte[] guide, int maxBytes, PhotoStyle style, string? instructions)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(guide);

        if (guide.Length == 0 || guide.Length > maxBytes)
        {
            return Constants.Messages.GuideImage;
        }

        if (plan.Items.Count == 0)
        {
            return Constants.Messages.EmptyPlan;
        }

        if (!Enum.IsDefined(style))
        {
            return Constants.Messages.UnknownPhotoStyle;
        }

        return (instructions?.Trim().Length ?? 0) > Constants.Builder.MaxInstructionsLength ? Constants.Messages.PhotoInstructions : null;
    }

    /// <summary>Whether boxes are few enough, each on the plan's objects and inside the picture; none fit too.</summary>
    /// <param name="boxes">The boxes, if any.</param>
    /// <param name="items">How many objects the plan has.</param>
    public static bool FitAll(IReadOnlyList<ItemBox>? boxes, int items)
    {
        return boxes is null || (boxes.Count <= Domain.Constants.Plans.MaxItems && boxes.All(box => Fits(box, items)));
    }

    /// <summary>A box as the image model takes it: an id, the object in words, the box, and where it is in the reference.</summary>
    /// <param name="plan">The room.</param>
    /// <param name="box">One object's box.</param>
    /// <param name="references">Where the objects are in the reference photo, if any.</param>
    public static LayoutBox Layout(RoomPlan plan, ItemBox box, IReadOnlyList<ItemBox>? references)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(box);

        var item = plan.Items[box.Item];
        var there = references?.FirstOrDefault(r => r.Item == box.Item);
        var from = there is null ? null : new SourceBox(there.Left, there.Top, there.Right, there.Bottom);

        return new LayoutBox($"{Constants.Builder.BoxLabel}{box.Item + 1}", PlanPrompt.Describe(item), box.Left, box.Top, box.Right, box.Bottom, from);
    }

    /// <summary>The room of the plan a new room replaces (Edit in 3D on a design's page).</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="replaces">The room asked for; none when missing.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The room, none when none was asked for, or why it can't be replaced.</returns>
    public async Task<Result<Room?>> ReplacedAsync(RoomPlan plan, Guid? replaces, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (replaces is not Guid id)
        {
            return Result.Success<Room?>(null);
        }

        var replaced = await rooms.FindAsync(id, currentUser.Id, cancellationToken);

        if (replaced is null)
        {
            return Result.Failure<Room?>(Constants.Messages.ReplacedRoomMissing);
        }

        return replaced.PlanId == plan.Id ? Result.Success<Room?>(replaced) : Result.Failure<Room?>(Constants.Messages.NotThisPlansRoom);
    }

    /// <summary>
    /// Puts a new room of the plan's photos into the plan's design, or into a replaced room's place (then deletes that
    /// room, only once the new one has its place). The photo is made and saved, so the design can't lose it: when the
    /// room can't go into the design now (it is full, or another run of the same plan made the design at the same
    /// moment), this is logged and the photo page puts it there.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="room">The new room, already saved.</param>
    /// <param name="replaced">The room it replaces, if any.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public async Task PlaceAsync(RoomPlan plan, Room room, Room? replaced, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(room);

        try
        {
            if (replaced is null)
            {
                await planDesigns.AddAsync(plan, room, cancellationToken);
            }
            else
            {
                await planDesigns.ReplaceAsync(plan, replaced, room, cancellationToken);
                await remover.DeleteAsync([replaced], cancellationToken);
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            LogNotInDesign(error, room.Id, plan.Id);
        }
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
}
