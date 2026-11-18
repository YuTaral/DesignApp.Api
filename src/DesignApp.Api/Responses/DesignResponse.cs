using DesignApp.Domain.Entities;


namespace DesignApp.Api.Responses;

/// <summary>A design as the API returns it.</summary>
/// <param name="Id">The design's id.</param>
/// <param name="Name">The design's name.</param>
/// <param name="Description">Its short description; empty when there is none.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="UpdatedAt">When it or one of its rooms was last changed.</param>
/// <param name="Rooms">Its rooms, in the order they were added.</param>
internal sealed record DesignResponse(
    Guid Id, string Name, string Description, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, List<DesignRoomResponse> Rooms)
{
    /// <summary>The response for a design.</summary>
    /// <param name="design">The design.</param>
    /// <param name="rooms">The user's rooms by id, for the photo each design room shows (missing: the uploaded photo).</param>
    public static DesignResponse From(Design design, IReadOnlyDictionary<Guid, Room> rooms)
    {
        return new(
            design.Id,
            design.Name,
            design.Description,
            design.CreatedAt,
            design.UpdatedAt,
            [.. design.Rooms.OrderBy(r => r.Position).Select(r => DesignRoomResponse.From(r, rooms.GetValueOrDefault(r.RoomId)))]);
    }
}
