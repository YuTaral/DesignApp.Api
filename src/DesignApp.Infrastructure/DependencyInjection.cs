using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Anthropic;
using DesignApp.Application.Interfaces;
using DesignApp.Infrastructure.Ai;
using DesignApp.Infrastructure.Ai.Claude;
using DesignApp.Infrastructure.Ai.ClaudeCode;
using DesignApp.Infrastructure.Ai.Fakes;
using DesignApp.Infrastructure.Ai.Fal;
using DesignApp.Infrastructure.Analysis;
using DesignApp.Infrastructure.Background;
using DesignApp.Infrastructure.Database;
using DesignApp.Infrastructure.FileStorage;
using DesignApp.Infrastructure.Library;
using DesignApp.Infrastructure.Builder;
using DesignApp.Infrastructure.Palettes;
using DesignApp.Infrastructure.Photos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace DesignApp.Infrastructure;

/// <summary>Registers the Infrastructure layer: database, photo storage and photo processing.</summary>
public static class DependencyInjection
{
    /// <summary>Adds Postgres (EF Core), S3-compatible storage, SkiaSharp imaging and the AI pipeline.</summary>
    /// <param name="builder">The host builder; reads the <c>designapp</c> connection string and the <c>Storage</c> and <c>Ai</c> sections.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        // Aspire's integration adds connection retries, health checks and OpenTelemetry tracing.
        builder.AddNpgsqlDbContext<AppDbContext>(Constants.Config.DatabaseName,
            configureDbContextOptions: options => options.UseSnakeCaseNamingConvention());
        builder.Services.AddScoped<IRoomRepository, RoomRepository>();
        builder.Services.AddScoped<IDesignRepository, DesignRepository>();
        builder.Services.AddScoped<IPlanRepository, PlanRepository>();
        builder.Services.AddSingleton<IObjectCatalog, EmbeddedObjectCatalog>();

        builder.Services.AddOptions<StorageOptions>()
            .Bind(builder.Configuration.GetSection(Constants.Config.StorageSection))
            .Validate(o => o.AccessKey.Length > 0 && o.SecretKey.Length > 0, Constants.Config.StorageKeysRequired)
            .ValidateOnStart();
        builder.Services.AddSingleton<IAmazonS3>(sp => CreateS3Client(sp.GetRequiredService<IOptions<StorageOptions>>().Value));
        builder.Services.AddSingleton<S3FileStorage>();
        builder.Services.AddSingleton<IFileStorage>(sp => sp.GetRequiredService<S3FileStorage>());

        builder.Services.AddSingleton<IPhotoProcessor, SkiaPhotoProcessor>();
        builder.Services.AddSingleton<IRoomImaging, SkiaRoomImaging>();
        builder.Services.AddSingleton<ILibrary, EmbeddedLibrary>();
        builder.Services.AddSingleton<IPaletteCatalog, EmbeddedPaletteCatalog>();

        AddAiPipeline(builder);

        return builder;
    }

    /// <summary>
    /// Adds the analysis queue, the background worker and the AI providers. The fakes are always
    /// there; Claude and SAM 3 on fal.ai whenever they are set up (required unless <c>Ai:UseFakes</c> is on):
    /// SAM 3 needs the fal.ai key, Claude the Anthropic key or, in Development only, <c>Ai:UseClaudeSubscription</c>
    /// (Claude Code on the developer's PC). Each room uses the mode it was uploaded with.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    private static void AddAiPipeline(IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<AiOptions>()
            .Bind(builder.Configuration.GetSection(Constants.Config.AiSection))
            .Validate(o => o.UseFakes || o.HasRealKeys, Constants.Config.AiKeysRequired)
            .Validate(o => !o.UseClaudeSubscription || builder.Environment.IsDevelopment(), Constants.Config.ClaudeSubscriptionDevelopmentOnly)
            .ValidateOnStart();

        builder.Services.AddSingleton<AnalysisScheduler>();
        builder.Services.AddSingleton<IAnalysisScheduler>(sp => sp.GetRequiredService<AnalysisScheduler>());
        builder.Services.AddHostedService<RoomAnalysisWorker>();
        builder.Services.AddHostedService<UnsavedRoomCleanupWorker>();

        builder.Services.AddSingleton<FakeInventoryProvider>();
        builder.Services.AddSingleton<FakeSegmentationProvider>();
        builder.Services.AddSingleton<FakeObjectRemovalProvider>();
        builder.Services.AddSingleton<FakeRoomBuilderAi>();
        builder.Services.AddSingleton<FakePhotoPlanner>();
        builder.Services.AddSingleton<FakeImageGenerator>();
        builder.Services.AddSingleton<IAiProviders, AiProviders>();
        builder.Services.AddSingleton<IAiSettingsStore, AiSettingsStore>();

        var configured = builder.Configuration.GetSection(Constants.Config.AiSection).Get<AiOptions>() ?? new AiOptions();

        if (!configured.HasRealKeys)
        {
            return;
        }

        if (configured.UseClaudeSubscription)
        {
            builder.Services.AddSingleton<IClaudeCodeRunner, ClaudeCodeRunner>();
            builder.Services.AddKeyedSingleton<IObjectInventoryProvider, ClaudeCodeInventoryProvider>(Constants.Ai.RealKey);
            builder.Services.AddSingleton<ClaudeCodeAsker>();
            builder.Services.AddKeyedSingleton<IRoomBuilderAi, ClaudeCodeRoomBuilderAi>(Constants.Ai.RealKey);
            builder.Services.AddKeyedSingleton<IPhotoPlanner, ClaudeCodePhotoPlanner>(Constants.Ai.RealKey);
        }
        else
        {
            AddClaudeApi(builder);
        }

        AddSam3(builder);
    }

    /// <summary>Claude through the API key: the production route.</summary>
    /// <param name="builder">The host builder.</param>
    private static void AddClaudeApi(IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IAnthropicClient>(sp =>
        {
            var ai = sp.GetRequiredService<IOptions<AiOptions>>().Value;
            var client = new AnthropicClient { ApiKey = ai.AnthropicApiKey };

            return string.IsNullOrWhiteSpace(ai.AnthropicWorkspaceId)
                ? client
                : client.WithOptions(options => options with
                {
                    ExtraHeaders = new Dictionary<string, string> { [Constants.Ai.WorkspaceHeader] = ai.AnthropicWorkspaceId.Trim() },
                });
        });
        builder.Services.AddKeyedSingleton<IObjectInventoryProvider, ClaudeInventoryProvider>(Constants.Ai.RealKey);
        builder.Services.AddKeyedSingleton<IRoomBuilderAi, ClaudeRoomBuilderAi>(Constants.Ai.RealKey);
        builder.Services.AddKeyedSingleton<IPhotoPlanner, ClaudePhotoPlanner>(Constants.Ai.RealKey);
    }

    /// <summary>SAM 3 and the object eraser on fal.ai, with the fal.ai key.</summary>
    /// <param name="builder">The host builder.</param>
    private static void AddSam3(IHostApplicationBuilder builder)
    {
        AddFalClient<FalSegmentationProvider>(builder);
        AddFalEraserClient(builder);
        AddFalImagesClient(builder);
        builder.Services.AddKeyedSingleton<IImageGenerator>(Constants.Ai.RealKey, (sp, _) => sp.GetRequiredService<FalImageGenerator>());
        builder.Services.AddKeyedSingleton<IObjectRemovalProvider>(Constants.Ai.RealKey, (sp, _) => sp.GetRequiredService<FalObjectRemovalProvider>());
        builder.Services.AddKeyedSingleton<ISegmentationProvider>(Constants.Ai.RealKey, (sp, _) => new RecordingSegmentationProvider(
            sp.GetRequiredService<FalSegmentationProvider>(),
            sp.GetRequiredService<IOptions<AiOptions>>(),
            sp.GetRequiredService<ILogger<RecordingSegmentationProvider>>()));
    }

    /// <summary>
    /// The eraser's HTTP client: the fal.ai key and a timeout per call, but no retries. Each submit is a paid job, so a
    /// slow answer must never send it again; the provider follows the queued job itself.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    private static void AddFalEraserClient(IHostApplicationBuilder builder)
    {
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is marked experimental
        builder.Services.AddHttpClient<FalObjectRemovalProvider>((sp, http) =>
                http.DefaultRequestHeaders.Authorization = new(Constants.Ai.FalAuthScheme, sp.GetRequiredService<IOptions<AiOptions>>().Value.FalKey))
            .RemoveAllResilienceHandlers()
            .AddResilienceHandler(Constants.Ai.Removal.ResilienceName, resilience => resilience.AddTimeout(Constants.Ai.Removal.CallTimeout));
#pragma warning restore EXTEXP0001
    }

    /// <summary>
    /// The image model's HTTP client, set up like the eraser's: the fal.ai key and a timeout per call, but no retries,
    /// since each submit is a paid job.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    private static void AddFalImagesClient(IHostApplicationBuilder builder)
    {
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is marked experimental
        builder.Services.AddHttpClient<FalImageGenerator>((sp, http) =>
                http.DefaultRequestHeaders.Authorization = new(Constants.Ai.FalAuthScheme, sp.GetRequiredService<IOptions<AiOptions>>().Value.FalKey))
            .RemoveAllResilienceHandlers()
            .AddResilienceHandler(Constants.Ai.Images.ResilienceName, resilience => resilience.AddTimeout(Constants.Ai.Images.CallTimeout));
#pragma warning restore EXTEXP0001
    }

    /// <summary>A typed HTTP client for a fal.ai model, with the fal.ai key.</summary>
    /// <typeparam name="TClient">The provider that uses it.</typeparam>
    /// <param name="builder">The host builder.</param>
    private static void AddFalClient<TClient>(IHostApplicationBuilder builder)
        where TClient : class
    {
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is marked experimental
        builder.Services.AddHttpClient<TClient>((sp, http) =>
                http.DefaultRequestHeaders.Authorization = new(Constants.Ai.FalAuthScheme, sp.GetRequiredService<IOptions<AiOptions>>().Value.FalKey))
            // SAM 3 on a large photo, or the eraser, can take well over the default 10 s per attempt, so replace
            // the shared resilience handler from ServiceDefaults.
            .RemoveAllResilienceHandlers()
            .AddStandardResilienceHandler(resilience =>
            {
                resilience.AttemptTimeout.Timeout = Constants.Ai.Resilience.AttemptTimeout;
                resilience.CircuitBreaker.SamplingDuration = Constants.Ai.Resilience.SamplingDuration;
                resilience.TotalRequestTimeout.Timeout = Constants.Ai.Resilience.TotalTimeout;
            });
#pragma warning restore EXTEXP0001
    }

    /// <summary>
    /// Prepares the database and storage before the API takes requests. Migrations run on startup
    /// only when <c>Database:MigrateOnStartup</c> is set (on in Development); production should
    /// apply them as a deployment step instead.
    /// </summary>
    /// <param name="services">The built app's services.</param>
    /// <param name="cancellationToken">Cancels the startup work.</param>
    public static async Task InitializeInfrastructureAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        if (configuration.GetValue<bool>(Constants.Config.MigrateOnStartup))
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(cancellationToken);
        }

        await scope.ServiceProvider.GetRequiredService<S3FileStorage>().EnsureBucketExistsAsync(cancellationToken);
    }

    /// <summary>Creates the S3 client for AWS or, when a service URL is set, a self-hosted store.</summary>
    /// <param name="options">Where the store is and how to sign in.</param>
    private static AmazonS3Client CreateS3Client(StorageOptions options)
    {
        var credentials = new BasicAWSCredentials(options.AccessKey, options.SecretKey);
        var config = new AmazonS3Config
        {
            // Only send and check checksums when S3 requires them: not every S3-compatible store supports the newer ones.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };

        if (string.IsNullOrEmpty(options.ServiceUrl))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
        }
        else
        {
            config.ServiceURL = options.ServiceUrl;
            config.AuthenticationRegion = options.Region;
            config.ForcePathStyle = true; // self-hosted stores don't do bucket subdomains
        }

        return new AmazonS3Client(credentials, config);
    }
}
