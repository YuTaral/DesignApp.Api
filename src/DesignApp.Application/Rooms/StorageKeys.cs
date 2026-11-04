namespace DesignApp.Application.Rooms;

/// <summary>One folder per room, so a room's photo, masks and label map live together.</summary>
public static class StorageKeys
{
    /// <summary>Storage key of a room's photo.</summary>
    /// <param name="roomId">The room's id.</param>
    public static string RoomPhoto(Guid roomId) => $"rooms/{roomId}/photo.jpg";
}
