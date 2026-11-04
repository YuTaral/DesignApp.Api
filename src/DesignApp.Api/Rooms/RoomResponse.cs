using DesignApp.Domain.Rooms;

namespace DesignApp.Api.Rooms;

/// <param name="PhotoUrl">Relative to the API's base URL.</param>
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

/// <param name="Box">Normalized to 0..1 of the photo's width and height.</param>
/// <param name="DominantColor">"#RRGGBB".</param>
internal sealed record DetectedObjectResponse(int Index, string Label, ObjectCategory Category, BoundingBoxResponse Box, string DominantColor)
{
    public static DetectedObjectResponse From(DetectedObject o) =>
        new(o.Index, o.Label, o.Category, new(o.Box.X, o.Box.Y, o.Box.Width, o.Box.Height), o.DominantColor.Value);
}

internal sealed record BoundingBoxResponse(double X, double Y, double Width, double Height);
