using DesignApp.Application.Interfaces;
using DesignApp.Application.UseCases.Room;
using Microsoft.Extensions.DependencyInjection;
using static DesignApp.Domain.Constants;

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
        var ai = scope.ServiceProvider.GetRequiredService<IAiProviders>();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AnalyzeRoom>());

        Assert.Equal("ClaudeInventoryProvider", ai.Inventory(AiMode.Real).GetType().Name);
        Assert.Equal("RecordingSegmentationProvider", ai.Segmentation(AiMode.Real).GetType().Name);
        Assert.Equal("FakeInventoryProvider", ai.Inventory(AiMode.Fake).GetType().Name);
        Assert.Equal(AiMode.Real, ai.Availability.Default);
    }

    [Fact]
    public void Without_keys_only_the_fakes_exist()
    {
        using var scope = factory.Services.CreateScope();
        var ai = scope.ServiceProvider.GetRequiredService<IAiProviders>();

        Assert.False(ai.Availability.RealAvailable);
        Assert.Throws<InvalidOperationException>(() => ai.Inventory(AiMode.Real));
    }
}
