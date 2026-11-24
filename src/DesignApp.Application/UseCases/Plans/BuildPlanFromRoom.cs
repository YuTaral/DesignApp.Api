using DesignApp.Application.Helpers;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.UseCases;

/// <summary>
/// Builds a plan from an uploaded room's photo, the reverse of Build a room: the room's AI reads the photo into a plan
/// (its type, size, surfaces and objects, picked from the catalog), <see cref="PhotoPlanReader"/> checks and tidies it,
/// and the plan is saved, linked to the room so the builder can show the photo beside it.
/// </summary>
/// <param name="rooms">Where rooms are stored.</param>
/// <param name="plans">Where plans are stored.</param>
/// <param name="storage">Where photos are kept.</param>
/// <param name="imaging">Prepares the photo for the AI.</param>
/// <param name="ai">The fake or real AI, per room.</param>
/// <param name="catalog">The objects a plan may use.</param>
/// <param name="currentUser">The signed-in user or guest.</param>
/// <param name="clock">The current time.</param>
/// <param name="planDesigns">Checks the design the room is started from.</param>
public sealed class BuildPlanFromRoom(
    IRoomRepository rooms, IPlanRepository plans, IFileStorage storage, IRoomImaging imaging, IAiProviders ai, IObjectCatalog catalog,
    ICurrentUser currentUser, TimeProvider clock, PlanDesigns planDesigns)
{
    /// <summary>Builds the plan.</summary>
    /// <param name="roomId">The uploaded room.</param>
    /// <param name="designId">The design the room is started from, whose photos go into it; none when missing.</param>
    /// <param name="newDesign">Without a design: the new design its photos go into, made with the first one.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The plan and what was left out; a failure the user can read when the AI couldn't read the photo; <c>null</c> when the user has no such room.</returns>
    public async Task<Result<PhotoPlanOutcome>?> HandleAsync(Guid roomId, Guid? designId, DesignDetails? newDesign, CancellationToken cancellationToken)
    {
        var room = await rooms.FindAsync(roomId, currentUser.Id, cancellationToken);

        if (room is null)
        {
            return null;
        }

        // The design first, so a refused one costs no AI call.
        var design = await planDesigns.ForNewPlanAsync(designId, currentUser.Id, cancellationToken);

        if (!design.Succeeded)
        {
            return Result.Failure<PhotoPlanOutcome>(design.Error);
        }

        var image = await AnalysisPhoto.ReadAsync(storage, imaging, room, cancellationToken);
        var offered = PhotoPlanReader.Offered(catalog);
        var read = await ai.PhotoPlanner(room.AiMode).PlanFromPhotoAsync(image, offered, cancellationToken);

        if (!read.Succeeded)
        {
            return Result.Failure<PhotoPlanOutcome>(read.Error);
        }

        var answer = read.Value;
        var ready = PhotoPlanReader.Read(answer, offered);
        var now = clock.GetUtcNow();
        var plan = RoomPlan.Create(Guid.CreateVersion7(now), currentUser.Id, "", answer.RoomType, ready.Width, ready.Length, ready.Height, ready.WallColor,
            ready.FloorColor, answer.FloorType, room.AiMode, now);

        plan.Update(plan.Name, plan.Outline, ready.Height, ready.WallColor, ready.FloorColor, answer.FloorType, new PlanView((int)PlanWall.Back, ViewCorner.Spot, ready.Camera),
            ready.Items, now, ready.Finish);
        plan.ReadFrom(room.Id);
        plan.ChooseDesign(design.Value, newDesign);
        plans.Add(plan);
        await plans.SaveChangesAsync(cancellationToken);

        return new PhotoPlanOutcome(plan, ready.LeftOut);
    }
}
