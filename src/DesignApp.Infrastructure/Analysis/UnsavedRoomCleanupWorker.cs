using DesignApp.Application.Rooms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DesignApp.Infrastructure.Analysis;

/// <summary>
/// Every hour, deletes the uploads nobody saved to a design within
/// <see cref="DeleteUnsavedRooms.Lifetime"/> (with their photos and outlines). It first runs a
/// minute after startup, so the API is up before it does any work.
/// </summary>
/// <param name="scopes">Creates a scope per pass, like a request.</param>
/// <param name="logger">Records what was deleted, and failures.</param>
internal sealed partial class UnsavedRoomCleanupWorker(IServiceScopeFactory scopes, ILogger<UnsavedRoomCleanupWorker> logger) : BackgroundService
{
    /// <summary>How often the cleanup runs.</summary>
    internal static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <summary>How long after startup the first pass waits.</summary>
    internal static readonly TimeSpan FirstRunDelay = TimeSpan.FromMinutes(1);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstRunDelay, stoppingToken);
            using var timer = new PeriodicTimer(Interval);
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The API is stopping.
        }
    }

    /// <summary>Deletes expired unsaved rooms, a batch at a time until none are left.</summary>
    /// <param name="cancellationToken">Cancels the work.</param>
    internal async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            int deleted;
            do
            {
                await using var scope = scopes.CreateAsyncScope();
                deleted = await scope.ServiceProvider.GetRequiredService<DeleteUnsavedRooms>().HandleAsync(cancellationToken);
                if (deleted > 0)
                    LogDeleted(deleted);
            }
            while (deleted == DeleteUnsavedRooms.BatchSize);
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested)
        {
            LogFailed(error);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} uploads that were never saved to a design")]
    private partial void LogDeleted(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Deleting unsaved uploads failed; trying again in an hour")]
    private partial void LogFailed(Exception error);
}
