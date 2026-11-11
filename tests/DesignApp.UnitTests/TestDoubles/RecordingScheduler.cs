using DesignApp.Application.Interfaces;


namespace DesignApp.UnitTests;

/// <summary>Records which rooms were scheduled for analysis.</summary>
internal sealed class RecordingScheduler : IAnalysisScheduler
{
    /// <summary>Scheduled room ids, in order.</summary>
    public List<Guid> Scheduled { get; } = [];

    /// <inheritdoc/>
    public void Schedule(Guid roomId)
    {
        Scheduled.Add(roomId);
    }
}
