using System.Threading.Channels;
using DesignApp.Application.Interfaces;

namespace DesignApp.Infrastructure.Analysis;

/// <summary>
/// In-memory hand-off from uploads to the worker. The database stays the source of truth: anything
/// queued but not finished when the API stops is found again by <see cref="Background.RoomAnalysisWorker"/> on start.
/// </summary>
internal sealed class AnalysisScheduler : IAnalysisScheduler
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    /// <summary>The worker's end of the queue.</summary>
    public ChannelReader<Guid> Reader => _channel.Reader;

    /// <inheritdoc/>
    public void Schedule(Guid roomId)
    {
        _channel.Writer.TryWrite(roomId);
    }
}
