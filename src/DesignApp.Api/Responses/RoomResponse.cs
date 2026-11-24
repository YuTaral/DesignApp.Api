using DesignApp.Domain.Entities;

using static DesignApp.Domain.Constants;

namespace DesignApp.Api.Responses;

/// <summary>A room as the API returns it to the frontend.</summary>
/// <param name="Id">The room's id.</param>
/// <param name="Status">Where the room is in the AI analysis.</param>
/// <param name="Width">Width of the stored photo, in pixels.</param>
/// <param name="Height">Height of the stored photo, in pixels.</param>
/// <param name="PhotoUrl">Where to load the current photo version, relative to the API's base URL.</param>
/// <param name="CreatedAt">When the photo was uploaded.</param>
/// <param name="FailureReason">Why the analysis failed; <c>null</c> unless <paramref name="Status"/> is Failed.</param>
/// <param name="Objects">The objects found in the photo; empty until the analysis is ready.</param>
/// <param name="LabelMapUrl">Where to load the current label map version (relative, like <paramref name="PhotoUrl"/>); <c>null</c> until ready.</param>
/// <param name="LabelMapRevision">The current label map version: 0 from the analysis, higher after outline fixes.</param>
/// <param name="Scope">Which objects the analysis looked for.</param>
/// <param name="AiMode">Which AI analyzed the photo.</param>
/// <param name="PhotoVersions">
/// For each label map version (by its number), which photo version goes with it: 0 is the uploaded photo
/// (<c>photo</c>), a higher one is <c>photo/{version}</c>, after an object was removed.
/// </param>
/// <param name="LibraryRoom">The built-in library layout it was copied from, such as <c>minimalist/kitchen/2</c>; <c>null</c> for an upload.</param>
/// <param name="PlanId">The room plan whose photos it holds; <c>null</c> for an upload or a library room.</param>
/// <param name="ChosenTakeId">Which of its photos to choose from is its photo; <c>null</c> when it has none.</param>
/// <param name="PhotoSetId">Named in its photo and outline addresses, new each time a photo is chosen; <c>null</c> when it has none.</param>
/// <param name="Panorama">Its 360-degree photo, whose views can be saved as its photos; <c>null</c> when it has none.</param>
public sealed record RoomResponse(
    Guid Id,
    RoomStatus Status,
    int Width,
    int Height,
    string PhotoUrl,
    DateTimeOffset CreatedAt,
    string? FailureReason,
    IReadOnlyList<DetectedObjectResponse> Objects,
    string? LabelMapUrl,
    int LabelMapRevision,
    AnalysisScope Scope,
    AiMode AiMode,
    IReadOnlyList<int> PhotoVersions,
    string? LibraryRoom,
    Guid? PlanId,
    Guid? ChosenTakeId,
    Guid? PhotoSetId,
    PanoramaResponse? Panorama)
{
    /// <summary>Maps a domain room to its API shape.</summary>
    /// <param name="room">The room to map.</param>
    public static RoomResponse From(Room room)
    {
        return new(
            room.Id,
            room.Status,
            room.Width,
            room.Height,
            Constants.Routes.RoomPhotoUrl(room.Id, room.PhotoVersion, room.PhotoSetId),
            room.CreatedAt,
            room.FailureReason,
            [.. room.Objects.Select(DetectedObjectResponse.From)],
            room.LabelMapKey is null ? null : Constants.Routes.RoomLabelMapUrl(room.Id, room.LabelMapRevision, room.PhotoSetId),
            room.LabelMapRevision,
            room.Scope,
            room.AiMode,
            [.. Enumerable.Range(0, room.LatestLabelMapRevision + 1).Select(room.PhotoVersionOf)],
            room.LibraryRoom,
            room.PlanId,
            room.ChosenTakeId,
            room.PhotoSetId,
            room.Panorama is { } panorama ? new PanoramaResponse(Constants.Routes.RoomPanoramaUrl(room.Id), panorama.Width, panorama.Height, panorama.StartYaw) : null);
    }
}
