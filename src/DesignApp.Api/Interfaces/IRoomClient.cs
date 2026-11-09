using DesignApp.Api.Responses;

namespace DesignApp.Api.Interfaces;

/// <summary>Messages the server sends to a browser watching a room.</summary>
public interface IRoomClient
{
    /// <summary>The room changed (a new analysis status, or the finished objects).</summary>
    /// <param name="room">The room's current state.</param>
    Task RoomChanged(RoomResponse room);
}
