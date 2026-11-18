using DesignApp.Api.Requests;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Objects;


namespace DesignApp.Api.Responses;

/// <summary>One room of a design.</summary>
/// <param name="RoomId">The room.</param>
/// <param name="PhotoUrl">Its current photo (with any removed objects gone), for a thumbnail.</param>
/// <param name="Edits">Its colors.</param>
/// <param name="Sizes">The sizes the user gave its walls, floors and ceilings.</param>
internal sealed record DesignRoomResponse(Guid RoomId, string PhotoUrl, List<EditRequest> Edits, List<SizeRequest> Sizes)
{
    /// <summary>The response for a room of a design.</summary>
    /// <param name="room">The room.</param>
    /// <param name="stored">Its room, for the photo it shows (its version, and its photo set); <c>null</c> for the uploaded photo.</param>
    public static DesignRoomResponse From(DesignRoom room, Room? stored)
    {
        return new(
            room.RoomId,
            Constants.Routes.RoomPhotoUrl(room.RoomId, stored?.PhotoVersion ?? 0, stored?.PhotoSetId),
            [.. room.Edits.OfType<RecolorEdit>().Select(e => new EditRequest(e.ObjectIndex, e.Color.Value, e.Strength, e.Paint,
                e.Tones.Count == 0 ? null : [.. e.Tones.Select(t => new ToneRequest(t.Center.Value, t.Share, t.Color?.Value, t.Strength, t.Paint))]))],
            [.. room.Sizes.Select(s => new SizeRequest(s.ObjectIndex, s.Width, s.Height))]);
    }
}
