using DesignApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DesignApp.Infrastructure.Ai;

/// <summary>
/// Reads the <c>ai_settings</c> row on every call. The AI providers are singletons, so each read
/// opens its own database scope; it is one small query per analysis step.
/// </summary>
/// <param name="scopes">Opens a scope for the database session.</param>
/// <param name="logger">Warns when the row is missing.</param>
internal sealed partial class AiSettingsStore(IServiceScopeFactory scopes, ILogger<AiSettingsStore> logger) : IAiSettingsStore
{
    /// <inheritdoc/>
    public async Task<AiSettings> GetAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.AiSettings.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == Constants.Persistence.AiSettingsRowId, cancellationToken);
        if (settings is null)
        {
            LogMissing();
            settings = new AiSettings();
        }

        var problems = settings.Problems();
        return problems.Count == 0 ? settings : throw new InvalidOperationException(string.Join(" ", problems));
    }

    /// <summary>Logs that the settings row is missing, so the defaults are used.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The ai_settings row is missing; the built-in defaults are used")]
    private partial void LogMissing();
}
