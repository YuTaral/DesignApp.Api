namespace DesignApp.Infrastructure.Ai;

/// <summary>Reads the AI settings the real AI uses.</summary>
internal interface IAiSettingsStore
{
    /// <summary>The current settings, read fresh so an edit applies to the next analysis.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Settings that passed <see cref="AiSettings.Problems"/>.</returns>
    /// <exception cref="InvalidOperationException">The stored settings can't be used.</exception>
    Task<AiSettings> GetAsync(CancellationToken cancellationToken);
}
