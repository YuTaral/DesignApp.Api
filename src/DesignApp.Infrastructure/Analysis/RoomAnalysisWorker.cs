using System.Collections.Concurrent;
using System.Threading.Channels;
using DesignApp.Application.Abstractions;
using DesignApp.Application.Analysis;
using DesignApp.Infrastructure.Ai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DesignApp.Infrastructure.Analysis;

/// <summary>
/// In-memory hand-off from uploads to the worker. The database stays the source of truth: anything
/// queued but not finished when the API stops is found again by <see cref="RoomAnalysisWorker"/> on start.
/// </summary>
internal sealed class AnalysisScheduler : IAnalysisScheduler
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    /// <summary>The worker's end of the queue.</summary>
    public ChannelReader<Guid> Reader => _channel.Reader;

    /// <inheritdoc/>
    public void Schedule(Guid roomId) => _channel.Writer.TryWrite(roomId);
}

/// <summary>
/// Background service that analyzes queued rooms, a few at a time. On start it re-queues rooms
/// whose analysis never finished, so a restart or crash doesn't leave rooms stuck.
/// </summary>
/// <param name="scheduler">Where uploads queue their rooms.</param>
/// <param name="scopes">Creates a scope (database session) per analysis.</param>
/// <param name="options">Supplies how many rooms run at once.</param>
/// <param name="logger">Records unexpected worker errors.</param>
internal sealed partial class RoomAnalysisWorker(
    AnalysisScheduler scheduler,
    IServiceScopeFactory scopes,
    IOptions<AiOptions> options,
    ILogger<RoomAnalysisWorker> logger) : BackgroundService
{
    /// <summary>Rooms being analyzed right now, so a room queued twice isn't analyzed twice at once.</summary>
    private readonly ConcurrentDictionary<Guid, byte> _inFlight = new();

    /// <summary>Re-queues unfinished rooms, then analyzes rooms as they arrive until the API stops.</summary>
    /// <param name="stoppingToken">Cancelled when the API shuts down.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeueUnfinishedAsync(stoppingToken);

        await Parallel.ForEachAsync(
            scheduler.Reader.ReadAllAsync(stoppingToken),
            new ParallelOptions { MaxDegreeOfParallelism = options.Value.MaxConcurrentAnalyses, CancellationToken = stoppingToken },
            AnalyzeAsync);
    }

    /// <summary>Queues every room whose analysis hasn't finished.</summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    private async Task RequeueUnfinishedAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var rooms = scope.ServiceProvider.GetRequiredService<IRoomRepository>();
        foreach (var id in await rooms.ListUnfinishedAsync(cancellationToken))
            scheduler.Schedule(id);
    }

    /// <summary>Analyzes one room in its own scope. Errors are logged so the worker keeps running.</summary>
    /// <param name="roomId">The room to analyze.</param>
    /// <param name="cancellationToken">Cancelled when the API shuts down.</param>
    private async ValueTask AnalyzeAsync(Guid roomId, CancellationToken cancellationToken)
    {
        if (!_inFlight.TryAdd(roomId, 0))
            return;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AnalyzeRoom>().HandleAsync(roomId, cancellationToken);
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested)
        {
            LogWorkerError(error, roomId);
        }
        finally
        {
            _inFlight.TryRemove(roomId, out _);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The analysis worker failed on room {RoomId}")]
    private partial void LogWorkerError(Exception error, Guid roomId);
}
