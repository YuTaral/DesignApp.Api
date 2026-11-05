using System.Security.Claims;
using DesignApp.Api.Accounts;
using DesignApp.Application.Abstractions;
using DesignApp.Application.Analysis;
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
/// <param name="rooms">Loads the room, checking that the caller owns it.</param>
internal sealed class RoomHub(IRoomRepository rooms) : Hub<IRoomClient>
{
    /// <summary>The hub's URL path.</summary>
    public const string Path = "/api/hubs/rooms";

    /// <summary>Starts sending a room's changes to this connection.</summary>
    /// <param name="roomId">The room to watch; it must belong to the caller.</param>
    /// <returns>The room's current state, so nothing that happened before watching is missed.</returns>
    /// <exception cref="HubException">The caller has no room with that id.</exception>
    public async Task<RoomResponse> Watch(Guid roomId)
    {
        var room = (Owner() is { } owner ? await rooms.FindAsync(roomId, owner, Context.ConnectionAborted) : null)
            ?? throw new HubException("We couldn't find that room.");
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(roomId), Context.ConnectionAborted);
        return RoomResponse.From(room);
    }

    /// <summary>
    /// Who the connection belongs to: the account it was opened with, else the guest whose cookie
    /// it sent. Read from the connection, not the current request: a hub method runs long after the
    /// request that opened the connection, and no cookie can be issued then.
    /// </summary>
    private string? Owner() =>
        Context.User?.Identity?.IsAuthenticated == true
            ? Context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            : Context.GetHttpContext() is { } http ? GuestCookie.Read(http) : null;

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
