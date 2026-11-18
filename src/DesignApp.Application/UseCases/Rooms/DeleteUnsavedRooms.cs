using DesignApp.Application.Helpers;
using DesignApp.Application.Interfaces;


namespace DesignApp.Application.UseCases;

/// <summary>
/// Deletes uploads that nobody saved: rooms older than <see cref="Constants.Rooms.UnsavedLifetime"/> that aren't in any
/// design. Only rooms in a design, and those a room plan was read from (its builder shows the photo), are kept for good.
/// </summary>
/// <param name="rooms">Finds the unsaved rooms.</param>
/// <param name="remover">Deletes them with their files.</param>
/// <param name="clock">Says what "older than" means now.</param>
public sealed class DeleteUnsavedRooms(IRoomRepository rooms, RoomRemover remover, TimeProvider clock)
{
    /// <summary>Deletes one batch of expired unsaved rooms.</summary>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many rooms were deleted.</returns>
    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var expired = await rooms.ListUnsavedBeforeAsync(clock.GetUtcNow() - Constants.Rooms.UnsavedLifetime, Constants.Rooms.CleanupBatchSize, cancellationToken);

        if (expired.Count > 0)
        {
            await remover.DeleteAsync(expired, cancellationToken);
        }

        return expired.Count;
    }
}
