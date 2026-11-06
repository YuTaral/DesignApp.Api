using DesignApp.Application.Analysis;
using DesignApp.Domain.Rooms;
using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests;

/// <summary>Records the status of every room change sent to the browser.</summary>
internal sealed class RecordingNotifier : IRoomNotifier
{
    /// <summary>The statuses sent, in order.</summary>
    public List<RoomStatus> Sent { get; } = [];

    /// <inheritdoc/>
    public Task RoomChangedAsync(Room room, CancellationToken cancellationToken)
    {
        Sent.Add(room.Status);
        return Task.CompletedTask;
    }
}
