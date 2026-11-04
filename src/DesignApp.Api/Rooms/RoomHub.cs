using DesignApp.Application.Analysis;
using DesignApp.Application.Rooms;
using DesignApp.Domain.Rooms;
using Microsoft.AspNetCore.SignalR;

namespace DesignApp.Api.Rooms;

/// <summary>Messages the server sends to a browser watching a room.</summary>
public interface IRoomClient
{
    /// <summary>The room changed (a new analysis status, or the finished objects).</summary>
    /// <param name="room">The room's current state.</param>
    Task RoomChanged(RoomResponse room);
}

/// <summary>
/// SignalR hub at <c>/api/hubs/rooms</c> for live analysis progress. The browser calls
/// <see cref="Watch"/> for the room it shows, then receives <see cref="IRoomClient.RoomChanged"/>.
/// </summary>
/// <param name="getRoom">Checks that the caller owns the room.</param>
internal sealed class RoomHub(GetRoom getRoom) : Hub<IRoomClient>
{
    /// <summary>The hub's URL path.</summary>
    public const string Path = "/api/hubs/rooms";

    /// <summary>Starts sending a room's changes to this connection.</summary>
    /// <param name="roomId">The room to watch; it must belong to the caller.</param>
    /// <returns>The room's current state, so nothing that happened before watching is missed.</returns>
    /// <exception cref="HubException">The caller has no room with that id.</exception>
    public async Task<RoomResponse> Watch(Guid roomId)
    {
        var room = await getRoom.HandleAsync(roomId, Context.ConnectionAborted)
            ?? throw new HubException("We couldn't find that room.");
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(roomId), Context.ConnectionAborted);
        return RoomResponse.From(room);
    }

    /// <summary>The SignalR group of everyone watching a room.</summary>
    /// <param name="roomId">The room's id.</param>
    public static string GroupName(Guid roomId) => $"rooms:{roomId}";
}

/// <summary><see cref="IRoomNotifier"/> that pushes room changes over <see cref="RoomHub"/>.</summary>
/// <param name="hub">Sends to the room's watchers.</param>
internal sealed class SignalRRoomNotifier(IHubContext<RoomHub, IRoomClient> hub) : IRoomNotifier
{
    /// <inheritdoc/>
    public Task RoomChangedAsync(Room room, CancellationToken cancellationToken) =>
        hub.Clients.Group(RoomHub.GroupName(room.Id)).RoomChanged(RoomResponse.From(room));
}
