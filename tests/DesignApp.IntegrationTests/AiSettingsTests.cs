using DesignApp.Infrastructure.Ai;
using DesignApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static DesignApp.Infrastructure.Constants;
using AiDefaults = DesignApp.Infrastructure.Constants.Ai.Defaults;

namespace DesignApp.IntegrationTests;

/// <summary>
/// The AI settings live in the one-row ai_settings table: the migration seeds today's values,
/// and an edit to the row applies to the next read without a restart.
/// </summary>
/// <param name="factory">The API on its own database (no analysis worker).</param>
public class AiSettingsTests(ApiFactoryWithoutAnalysis factory) : IClassFixture<ApiFactoryWithoutAnalysis>
{
    [Fact]
    public async Task The_migration_seeds_the_built_in_defaults()
    {
        var settings = await StoreAsync();

        Assert.Equal(AiDefaults.InventoryModel, settings.InventoryModel);
        Assert.Equal(AiDefaults.InventoryEffort, settings.InventoryEffort);
        Assert.Equal(AiDefaults.MaxTokens, settings.MaxTokens);
        Assert.Equal(AiDefaults.Instructions, settings.Instructions);
        Assert.Equal(AiDefaults.SegmentationEndpoint, settings.SegmentationEndpoint);
        Assert.Equal(AiDefaults.MinShareInsideBox, settings.MinShareInsideBox);
    }

    [Fact]
    public async Task An_edit_to_the_row_applies_to_the_next_read()
    {
        try
        {
            await UpdateAsync("update ai_settings set inventory_model = 'claude-opus-5-5', max_points = 2, inventory_effort = 'High'");

            var settings = await StoreAsync();

            Assert.Equal("claude-opus-5-5", settings.InventoryModel);
            Assert.Equal(InventoryEffort.High, settings.InventoryEffort);
            Assert.Contains("2 or 2 points", settings.SystemPrompt(), StringComparison.Ordinal);
        }
        finally
        {
            await ResetAsync();
        }
    }

    [Fact]
    public async Task Unusable_values_stop_the_analysis_with_the_reason()
    {
        try
        {
            await UpdateAsync("update ai_settings set max_tokens = 0, segmentation_endpoint = 'http://example.com'");

            var error = await Assert.ThrowsAsync<InvalidOperationException>(StoreAsync);

            Assert.Contains("MaxTokens", error.Message, StringComparison.Ordinal);
            Assert.Contains("SegmentationEndpoint", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            await ResetAsync();
        }
    }

    [Fact]
    public async Task The_table_holds_only_one_row()
    {
        await Assert.ThrowsAnyAsync<DbUpdateException>(async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AiSettings.Add(new AiSettings { Id = 2 });
            await db.SaveChangesAsync();
        });
    }

    /// <summary>Reads the settings the way the AI providers do.</summary>
    private Task<AiSettings> StoreAsync()
    {
        return factory.Services.GetRequiredService<IAiSettingsStore>().GetAsync(CancellationToken.None);
    }

    /// <summary>Changes the row the way someone in pgAdmin would.</summary>
    /// <param name="sql">The update.</param>
    private async Task UpdateAsync(string sql)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(sql);
    }

    /// <summary>Puts the defaults back for the other tests in this class.</summary>
    private async Task ResetAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.AiSettings.SingleAsync();
        db.Entry(row).CurrentValues.SetValues(new AiSettings());
        await db.SaveChangesAsync();
    }
}
