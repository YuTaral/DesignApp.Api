using DesignApp.Api.Requests;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Objects;


namespace DesignApp.Api.Responses;

/// <summary>One room of a design.</summary>
/// <param name="RoomId">The room.</param>
/// <param name="PhotoUrl">Its photo, for a thumbnail.</param>
/// <param name="Edits">Its colors.</param>
internal sealed record DesignRoomResponse(Guid RoomId, string PhotoUrl, List<EditRequest> Edits)
{
    /// <summary>The response for a room of a design.</summary>
    /// <param name="room">The room.</param>
    public static DesignRoomResponse From(DesignRoom room) => new(
        room.RoomId,
        Constants.Routes.RoomPhotoUrl(room.RoomId),
        [.. room.Edits.OfType<RecolorEdit>().Select(e => new EditRequest(e.ObjectIndex, e.Color.Value, e.Strength))]);
}
