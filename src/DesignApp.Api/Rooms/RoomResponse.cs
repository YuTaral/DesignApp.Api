using DesignApp.Domain.Rooms;

namespace DesignApp.Api.Rooms;

/// <summary>A room as the API returns it to the frontend.</summary>
/// <param name="Id">The room's id.</param>
/// <param name="Status">Where the room is in the AI analysis.</param>
/// <param name="Width">Width of the stored photo, in pixels.</param>
/// <param name="Height">Height of the stored photo, in pixels.</param>
/// <param name="PhotoUrl">Where to load the photo, relative to the API's base URL.</param>
/// <param name="CreatedAt">When the photo was uploaded.</param>
/// <param name="FailureReason">Why the analysis failed; <c>null</c> unless <paramref name="Status"/> is Failed.</param>
/// <param name="Objects">The objects found in the photo; empty until the analysis is ready.</param>
internal sealed record RoomResponse(
    Guid Id,
    RoomStatus Status,
    int Width,
    int Height,
    string PhotoUrl,
    DateTimeOffset CreatedAt,
    string? FailureReason,
    IReadOnlyList<DetectedObjectResponse> Objects)
{
    /// <summary>Maps a domain room to its API shape.</summary>
    /// <param name="room">The room to map.</param>
    public static RoomResponse From(Room room) => new(
        room.Id,
        room.Status,
        room.Width,
        room.Height,
        $"/api/rooms/{room.Id}/photo",
        room.CreatedAt,
        room.FailureReason,
        [.. room.Objects.Select(DetectedObjectResponse.From)]);
}

/// <summary>One detected object as the API returns it.</summary>
/// <param name="Index">The object's value in the label map.</param>
/// <param name="Label">What the object is, such as "sofa".</param>
/// <param name="Category">The kind of object, such as Wall or Furniture.</param>
/// <param name="Box">Where the object is, normalized to 0..1 of the photo's width and height.</param>
/// <param name="DominantColor">The object's main color as "#RRGGBB".</param>
internal sealed record DetectedObjectResponse(int Index, string Label, ObjectCategory Category, BoundingBoxResponse Box, string DominantColor)
{
    /// <summary>Maps a domain object to its API shape.</summary>
    /// <param name="o">The object to map.</param>
    public static DetectedObjectResponse From(DetectedObject o) =>
        new(o.Index, o.Label, o.Category, new(o.Box.X, o.Box.Y, o.Box.Width, o.Box.Height), o.DominantColor.Value);
}

/// <summary>A box in normalized image coordinates (0..1).</summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Width as a fraction of the photo's width.</param>
/// <param name="Height">Height as a fraction of the photo's height.</param>
internal sealed record BoundingBoxResponse(double X, double Y, double Width, double Height);
