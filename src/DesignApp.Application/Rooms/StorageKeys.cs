namespace DesignApp.Application.Rooms;

/// <summary>One folder per room, so a room's photo, masks and label map live together.</summary>
public static class StorageKeys
{
    /// <summary>Storage key of a room's photo.</summary>
    /// <param name="roomId">The room's id.</param>
    public static string RoomPhoto(Guid roomId) => $"rooms/{roomId}/photo.jpg";

    /// <summary>Storage key of a room's label map, as the analysis made it or after outline fixes.</summary>
    /// <param name="roomId">The room's id.</param>
    /// <param name="revision">The label map version: 0 from the analysis, then one per fix.</param>
    public static string RoomLabelMap(Guid roomId, int revision = 0) =>
        revision == 0 ? $"rooms/{roomId}/labels.png" : $"rooms/{roomId}/labels-{revision}.png";
}
