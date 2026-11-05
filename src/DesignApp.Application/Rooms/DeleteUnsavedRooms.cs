using DesignApp.Application.Abstractions;

namespace DesignApp.Application.Rooms;

/// <summary>
/// Deletes uploads that nobody saved: rooms older than <see cref="Lifetime"/> that aren't in any
/// design. Only rooms in a design are kept for good.
/// </summary>
/// <param name="rooms">Finds the unsaved rooms.</param>
/// <param name="remover">Deletes them with their files.</param>
/// <param name="clock">Says what "older than" means now.</param>
public sealed class DeleteUnsavedRooms(IRoomRepository rooms, RoomRemover remover, TimeProvider clock)
{
    /// <summary>How long an upload is kept without being saved to a design: long enough to edit it, reload, or sign in.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    /// <summary>How many rooms one pass deletes, so a big backlog is cleared in steps.</summary>
    public const int BatchSize = 100;

    /// <summary>Deletes one batch of expired unsaved rooms.</summary>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many rooms were deleted.</returns>
    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var expired = await rooms.ListUnsavedBeforeAsync(clock.GetUtcNow() - Lifetime, BatchSize, cancellationToken);
        if (expired.Count > 0)
            await remover.DeleteAsync(expired, cancellationToken);
        return expired.Count;
    }
}
