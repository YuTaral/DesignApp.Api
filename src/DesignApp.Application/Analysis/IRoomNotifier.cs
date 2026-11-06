using DesignApp.Domain.Rooms;

namespace DesignApp.Application.Analysis;

/// <summary>Tells the browser that a room changed (live progress over SignalR).</summary>
public interface IRoomNotifier
{
    /// <summary>Sends the room's current state to everyone watching it.</summary>
    /// <param name="room">The changed room.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    Task RoomChangedAsync(Room room, CancellationToken cancellationToken);
}
