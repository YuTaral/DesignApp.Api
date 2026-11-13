namespace DesignApp.Api.Requests;

/// <summary>Body of <c>PUT /api/designs/{id}/rooms/{roomId}</c>.</summary>
/// <param name="Edits">The room's new colors, replacing the old ones.</param>
/// <param name="Sizes">The sizes of its walls, floors and ceilings, replacing the old ones.</param>
internal sealed record RoomColorsRequest(List<EditRequest>? Edits, List<SizeRequest>? Sizes = null);
