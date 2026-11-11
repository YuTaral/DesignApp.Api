using DesignApp.Api.Hubs;
using DesignApp.Api.Interfaces;
using DesignApp.Api.Responses;
using DesignApp.Application.Interfaces;
using DesignApp.Domain.Entities;
using Microsoft.AspNetCore.SignalR;

namespace DesignApp.Api.Services;

/// <summary><see cref="IRoomNotifier"/> that pushes room changes over <see cref="RoomHub"/>.</summary>
/// <param name="hub">Sends to the room's watchers.</param>
internal sealed class SignalRRoomNotifier(IHubContext<RoomHub, IRoomClient> hub) : IRoomNotifier
{
    /// <inheritdoc/>
    public Task RoomChangedAsync(Room room, CancellationToken cancellationToken)
    {
        return hub.Clients.Group(Constants.Hub.Group(room.Id)).RoomChanged(RoomResponse.From(room));
    }
}
