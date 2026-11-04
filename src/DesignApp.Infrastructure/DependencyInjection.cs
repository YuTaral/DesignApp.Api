using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using DesignApp.Application.Abstractions;
using DesignApp.Application.Photos;
using DesignApp.Infrastructure.Persistence;
using DesignApp.Infrastructure.Photos;
using DesignApp.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DesignApp.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Name of the Postgres database resource in the AppHost (and its connection string).</summary>
    public const string DatabaseName = "designapp";

    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        // Aspire's integration adds connection retries, health checks and OpenTelemetry tracing.
        builder.AddNpgsqlDbContext<AppDbContext>(DatabaseName,
            configureDbContextOptions: options => options.UseSnakeCaseNamingConvention());
        builder.Services.AddScoped<IRoomRepository, RoomRepository>();

        builder.Services.AddOptions<StorageOptions>()
            .Bind(builder.Configuration.GetSection(StorageOptions.Section))
            .Validate(o => o.AccessKey.Length > 0 && o.SecretKey.Length > 0, "Storage:AccessKey and Storage:SecretKey are required.")
            .ValidateOnStart();
        builder.Services.AddSingleton<IAmazonS3>(sp => CreateS3Client(sp.GetRequiredService<IOptions<StorageOptions>>().Value));
        builder.Services.AddSingleton<S3FileStorage>();
        builder.Services.AddSingleton<IFileStorage>(sp => sp.GetRequiredService<S3FileStorage>());

        builder.Services.AddSingleton<IPhotoProcessor, SkiaPhotoProcessor>();
        return builder;
    }

    /// <summary>
    /// Prepares the database and storage before the API takes requests. Migrations run on startup
    /// only when <c>Database:MigrateOnStartup</c> is set (on in Development); production should
    /// apply them as a deployment step instead.
    /// </summary>
    public static async Task InitializeInfrastructureAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        if (configuration.GetValue<bool>("Database:MigrateOnStartup"))
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(cancellationToken);

        await scope.ServiceProvider.GetRequiredService<S3FileStorage>().EnsureBucketExistsAsync(cancellationToken);
    }

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
