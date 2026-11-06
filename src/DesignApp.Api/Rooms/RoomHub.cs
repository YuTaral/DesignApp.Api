using System.Security.Claims;
using DesignApp.Api.Accounts;
using DesignApp.Application.Abstractions;
using DesignApp.Domain.Rooms;
using Microsoft.AspNetCore.SignalR;

namespace DesignApp.Api.Rooms;

/// <summary>
/// SignalR hub at <c>/api/hubs/rooms</c> for live analysis progress. The browser calls
/// <see cref="Watch"/> for the room it shows, then receives <see cref="IRoomClient.RoomChanged"/>.
/// </summary>
/// <param name="rooms">Loads the room, checking that the caller owns it.</param>
internal sealed class RoomHub(IRoomRepository rooms) : Hub<IRoomClient>
{
    /// <summary>Starts sending a room's changes to this connection.</summary>
    /// <param name="roomId">The room to watch; it must belong to the caller.</param>
    /// <returns>
    /// The room's current state, so nothing that happened before watching is missed; <c>null</c> when
    /// the connection closed meanwhile.
    /// </returns>
    /// <exception cref="HubException">The caller has no room with that id.</exception>
    public async Task<RoomResponse?> Watch(Guid roomId)
    {
        Room? room;
        try
        {
            room = Owner() is { } owner ? await rooms.FindAsync(roomId, owner, Context.ConnectionAborted) : null;
            if (room is not null)
                await Groups.AddToGroupAsync(Context.ConnectionId, Constants.Hub.Group(roomId), Context.ConnectionAborted);
        }
        catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
        {
            // The browser left (page closed or the analysis finished) while this ran; no one is
            // waiting for the answer, and it's not an error.
            return null;
        }
        return room is null ? throw new HubException(Constants.Messages.RoomNotFound) : RoomResponse.From(room);
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
}
