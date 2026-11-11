namespace DesignApp.UnitTests;

/// <summary>A clock that always returns the same time.</summary>
/// <param name="now">The time to return.</param>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow()
    {
        return now;
    }
}
