using DesignApp.Api.Requests;
using DesignApp.Domain.Entities;

using static DesignApp.Domain.Constants;

namespace DesignApp.Api.Responses;

/// <summary>A built room as the API returns it.</summary>
/// <param name="Id">Its id.</param>
/// <param name="Name">Its name.</param>
/// <param name="RoomType">What kind of room it is.</param>
/// <param name="Width">The width, in metres.</param>
/// <param name="Length">The length, in metres.</param>
/// <param name="Height">The ceiling height, in metres.</param>
/// <param name="WallColor">The walls' color.</param>
/// <param name="FloorColor">The floor's color.</param>
/// <param name="FloorType">What the floor is made of.</param>
/// <param name="View">The number of the wall the 3D view and the photo look at.</param>
/// <param name="AiMode">The AI its photos are made with.</param>
/// <param name="Items">The objects placed in it.</param>
/// <param name="UpdatedAt">When it was last saved.</param>
/// <param name="Outline">Its walls' corners, clockwise from the top-left one, in centimetres.</param>
/// <param name="Corner">Where the camera stands to look at the wall in view: across from its middle, or in a near corner.</param>
/// <param name="DesignId">The plan's own design, which its photos' rooms go into; <c>null</c> until there is one.</param>
/// <param name="Finish">How the walls and ceiling are finished and lit, and when the photo is taken.</param>
/// <param name="FromRoomId">The uploaded room whose photo the plan was read from; <c>null</c> for a plan built from scratch.</param>
/// <param name="CameraSpot">Where that photo was taken, in centimetres, for the <c>Spot</c> camera; <c>null</c> when there is none.</param>
public sealed record PlanResponse(
    Guid Id, string Name, RoomType RoomType, double Width, double Length, double Height, string WallColor, string FloorColor,
    FloorType FloorType, int View, AiMode AiMode, List<PlanItemRequest> Items, DateTimeOffset UpdatedAt, List<OutlinePointRequest> Outline, ViewCorner Corner,
    Guid? DesignId, RoomFinishRequest Finish, Guid? FromRoomId, OutlinePointRequest? CameraSpot)
{
    /// <summary>Maps a plan to its API shape.</summary>
    /// <param name="plan">The plan.</param>
    public static PlanResponse From(RoomPlan plan)
    {
        var items = plan.Items
            .Select(i => new PlanItemRequest(i.Label, i.Shape, i.X, i.Y, i.Width, i.Depth, i.Height, i.Rotation, i.Color.Value, i.Wall, i.Elevation, i.Form, ItemPartsRequest.From(i.Parts), i.Decorations.Names, i.Material, WindowCoveringRequest.From(i.Covering),
                WorktopRequest.From(i.Worktop), i.Lit))
            .ToList();
        var outline = plan.Outline.Points.Select(p => new OutlinePointRequest(p.X, p.Y)).ToList();
        var spot = plan.CameraSpot is { } at ? new OutlinePointRequest(at.X, at.Y) : null;

        return new(plan.Id, plan.Name, plan.RoomType, plan.Width, plan.Length, plan.Height, plan.WallColor.Value, plan.FloorColor.Value,
            plan.FloorType, plan.View, plan.AiMode, items, plan.UpdatedAt, outline, plan.Corner, plan.DesignId, RoomFinishRequest.From(plan.Finish), plan.FromRoomId, spot);
    }
}
