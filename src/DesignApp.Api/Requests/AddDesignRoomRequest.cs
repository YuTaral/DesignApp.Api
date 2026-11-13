namespace DesignApp.Api.Requests;

/// <summary>Body of <c>POST /api/designs/{id}/rooms</c>.</summary>
/// <param name="RoomId">The room to add.</param>
/// <param name="Edits">Its colors; none for a room still being analyzed.</param>
/// <param name="Sizes">The sizes of its walls, floors and ceilings; none for a room still being analyzed.</param>
internal sealed record AddDesignRoomRequest(Guid RoomId, List<EditRequest>? Edits, List<SizeRequest>? Sizes = null);
