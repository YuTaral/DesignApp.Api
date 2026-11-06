namespace DesignApp.Application.Analysis;

/// <summary>Hands rooms to the background worker that analyzes them.</summary>
public interface IAnalysisScheduler
{
    /// <summary>Queues a room for analysis. Never blocks.</summary>
    /// <param name="roomId">The room to analyze.</param>
    void Schedule(Guid roomId);
}
