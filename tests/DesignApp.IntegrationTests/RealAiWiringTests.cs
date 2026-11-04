using DesignApp.Application.Analysis;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace DesignApp.IntegrationTests;

/// <summary>
/// The API starts with the real AI providers configured (fakes off), with and without an
/// Anthropic workspace id. Nothing calls Claude or fal.ai: the keys are placeholders.
/// </summary>
/// <param name="factory">The API running against test containers.</param>
public class RealAiWiringTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData(null)]
    [InlineData("wrkspc_test")]
    public void The_real_providers_can_be_created(string? workspaceId)
    {
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Ai:UseFakes", "false");
            builder.UseSetting("Ai:AnthropicApiKey", "sk-ant-placeholder");
            builder.UseSetting("Ai:FalKey", "placeholder:placeholder");
            if (workspaceId is not null)
                builder.UseSetting("Ai:AnthropicWorkspaceId", workspaceId);
        });

        using var scope = app.Services.CreateScope();
        var inventory = scope.ServiceProvider.GetRequiredService<IObjectInventoryProvider>();
        var segmentation = scope.ServiceProvider.GetRequiredService<ISegmentationProvider>();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AnalyzeRoom>());

        Assert.Equal("ClaudeInventoryProvider", inventory.GetType().Name);
        Assert.Equal("RecordingSegmentationProvider", segmentation.GetType().Name);
    }
}
