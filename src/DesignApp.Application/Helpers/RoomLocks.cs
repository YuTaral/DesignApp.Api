using System.Collections.Concurrent;

namespace DesignApp.Application.Helpers;

/// <summary>
/// One lock per room, for work that must not run twice at once on the same room: measuring its depth is a paid call
/// whose result is stored, so a second request waits and then finds it done. Shared by the whole process (a singleton).
/// </summary>
public sealed class RoomLocks
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    /// <summary>Waits until no other request works on the room, then holds it until disposed.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>What releases the room when disposed.</returns>
    public async Task<IDisposable> LockAsync(Guid roomId, CancellationToken cancellationToken)
    {
        var gate = _locks.GetOrAdd(roomId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new Release(gate);
    }

    /// <summary>Releases a room's lock once.</summary>
    /// <param name="gate">The room's lock.</param>
    private sealed class Release(SemaphoreSlim gate) : IDisposable
    {
        private int _released;

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                gate.Release();
        }
    }
}
