using static DesignApp.Domain.Constants;

namespace DesignApp.Application.Objects;

/// <summary>Which AI modes this API can use, and which one an upload gets when it doesn't choose.</summary>
/// <param name="Default">The mode of an upload that doesn't choose.</param>
/// <param name="CanChoose">Whether uploads may choose (Development only: production always uses the real AI).</param>
/// <param name="RealAvailable">Whether the real AI is set up (both API keys present).</param>
public sealed record AiAvailability(AiMode Default, bool CanChoose, bool RealAvailable)
{
    /// <summary>Whether an upload may use a mode.</summary>
    /// <param name="mode">The mode asked for.</param>
    public bool Allows(AiMode mode) => mode == Default || (CanChoose && (mode is AiMode.Fake || RealAvailable));
}
