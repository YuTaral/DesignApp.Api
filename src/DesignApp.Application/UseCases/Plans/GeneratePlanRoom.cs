using DesignApp.Application.Helpers;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.UseCases;

/// <summary>
/// Turns a built room into a realistic photo: the prompt is written from the plan with its exact sizes and positions
/// (<see cref="PlanPrompt"/>), and the image model turns the room's simple 3D view into a photo with that layout. The
/// photos of one Generate run are one room: the first makes the room (its photo, the room's), the others are added to
/// it to choose from, each made with the run's first photo as a reference: the objects it shows are copied from it, so
/// every view has the same furniture, materials and decorations. Its analysis waits until the user starts it (with the
/// real AI it costs money). A signed-in user's
/// room goes into the plan's own design, or, when the run replaces an older room of the plan (Edit in 3D on a design's
/// page), into that room's place, and the old room is deleted.
/// </summary>
/// <param name="plans">Where plans are stored.</param>
/// <param name="rooms">Where rooms and their photos are stored.</param>
/// <param name="currentUser">The signed-in user or guest.</param>
/// <param name="ai">The AI providers.</param>
/// <param name="takes">Stores the photos.</param>
/// <param name="planPhotos">Checks the request, and puts the room into the plan's design or a replaced room's place.</param>
/// <param name="clock">When the room is made.</param>
public sealed class GeneratePlanRoom(
    IPlanRepository plans, IRoomRepository rooms, ICurrentUser currentUser, IAiProviders ai, TakeStore takes, PlanPhotos planPhotos, TimeProvider clock)
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
    /// <param name="run">The Generate run it belongs to, what it is, and the room it replaces, if any.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The room (waiting for its analysis), a failure the user can read, or <c>null</c> when the user has no such plan.</returns>
    public async Task<Result<Room>?> HandleAsync(
        Guid planId, byte[] guidePng, PhotoStyle style, string? instructions, int? seed, IReadOnlyList<ItemBox>? boxes, PlanShot? shot,
        PhotoRun run, CancellationToken cancellationToken)
    {
        var plan = await plans.FindAsync(planId, currentUser.Id, cancellationToken);

        if (plan is null)
        {
            return null;
        }

        var room = run.RoomId is Guid id ? await rooms.FindAsync(id, currentUser.Id, cancellationToken) : null;
        // Only the run's first photo makes a room, which can replace an older one.
        var replaced = await planPhotos.ReplacedAsync(plan, run.RoomId is null ? run.Replaces : null, cancellationToken);

        if (run.RoomId is not null && room?.PlanId != plan.Id)
        {
            return Result.Failure<Room>(Constants.Messages.NotThisPlansRoom);
        }

        if (!replaced.Succeeded)
        {
            return Result.Failure<Room>(replaced.Error);
        }

        if ((run.Note?.Trim().Length ?? 0) > Domain.Constants.Rooms.MaxTakeNoteLength)
        {
            return Result.Failure<Room>(Constants.Messages.PhotoNote);
        }

        if (PlanPhotos.Problem(plan, guidePng, Constants.Builder.MaxGuideBytes, style, instructions) is { } problem)
        {
            return Result.Failure<Room>(problem);
        }

        if (seed < 0)
        {
            return Result.Failure<Room>(Constants.Messages.PhotoSeed);
        }

        if (!PlanPhotos.FitAll(boxes, plan.Items.Count) || !PlanPhotos.FitAll(run.ReferenceBoxes, plan.Items.Count))
        {
            return Result.Failure<Room>(Constants.Messages.PhotoBoxes);
        }

        if (shot is not null && (shot.View < 0 || shot.View >= plan.Outline.Walls.Count || !Enum.IsDefined(shot.Corner)))
        {
            return Result.Failure<Room>(Constants.Messages.PhotoShot);
        }

        // A later photo of the run takes the run's first photo (the room's first) as its reference.
        var first = room is null ? null : (await rooms.ListTakesAsync(room.Id, cancellationToken)).FirstOrDefault(t => t.ChangedFrom is null);
        var reference = first is null ? null : await takes.ReadAsync(first, cancellationToken);
        var prompt = PlanPrompt.Write(plan, style, instructions, shot, reference is not null);
        var layout = boxes?.Select(box => PlanPhotos.Layout(plan, box, run.ReferenceBoxes)).ToList();
        var photo = await ai.Images(plan.AiMode).GenerateAsync(guidePng, reference, prompt, seed, layout, null, cancellationToken);

        if (!photo.Succeeded)
        {
            return Result.Failure<Room>(photo.Error);
        }

        var newId = room?.Id ?? Guid.CreateVersion7(clock.GetUtcNow());
        var stored = await takes.StoreAsync(newId, photo.Value, run.Note ?? "", null, cancellationToken);

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

        await planPhotos.PlaceAsync(plan, room, replaced.Value, cancellationToken);

        return room;
    }

}
