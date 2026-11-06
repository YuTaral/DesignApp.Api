namespace DesignApp.Api.Rooms;

/// <summary>The body of <c>PUT /api/rooms/{id}/labels/current</c>.</summary>
/// <param name="Revision">The stored label map version to make current.</param>
public sealed record LabelMapRevisionRequest(int Revision);
