using DesignApp.Application.Analysis;
using DesignApp.Domain.Rooms;
using Microsoft.AspNetCore.SignalR;

namespace DesignApp.Api.Rooms;

/// <summary><see cref="IRoomNotifier"/> that pushes room changes over <see cref="RoomHub"/>.</summary>
/// <param name="hub">Sends to the room's watchers.</param>
internal sealed class SignalRRoomNotifier(IHubContext<RoomHub, IRoomClient> hub) : IRoomNotifier
{
    /// <inheritdoc/>
    public Task RoomChangedAsync(Room room, CancellationToken cancellationToken) =>
        hub.Clients.Group(Constants.Hub.Group(room.Id)).RoomChanged(RoomResponse.From(room));
}
