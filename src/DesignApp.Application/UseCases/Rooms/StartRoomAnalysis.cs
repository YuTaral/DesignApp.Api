using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.UseCases;

/// <summary>
/// Use case: start the analysis of a room that waits for the user (a photo made by the room builder), so the real AI is
/// only paid for when the user asks for it.
/// </summary>
/// <param name="rooms">The room database.</param>
/// <param name="currentUser">Whose rooms may be analyzed.</param>
/// <param name="analysisScheduler">Starts the AI analysis once the room is saved.</param>
public sealed class StartRoomAnalysis(IRoomRepository rooms, ICurrentUser currentUser, IAnalysisScheduler analysisScheduler)
{
    /// <summary>Queues the room's analysis.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The room, now queued; a failure when it isn't waiting; or <c>null</c> when the user has no such room.</returns>
    public async Task<Result<Room>?> HandleAsync(Guid roomId, CancellationToken cancellationToken)
    {
        var room = await rooms.FindAsync(roomId, currentUser.Id, cancellationToken);
        if (room is null)
            return null;
        if (room.Status is not RoomStatus.Waiting)
            return Result.Failure<Room>(Constants.Messages.NotWaiting);

        room.QueueAnalysis();
        await rooms.SaveChangesAsync(cancellationToken);
        analysisScheduler.Schedule(room.Id);
        return room;
    }
}
