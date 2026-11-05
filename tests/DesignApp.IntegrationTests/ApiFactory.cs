using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace DesignApp.IntegrationTests;

/// <summary>
/// Runs the real API against real Postgres and real S3-compatible storage in throwaway containers,
/// so the tests cover the SQL and S3 calls rather than in-memory stand-ins.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const int S3Port = 9000;
    private const string StorageUser = "integration";
    private const string StoragePassword = "integration-secret";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    private readonly IContainer _storage = new ContainerBuilder("rustfs/rustfs:1.0.4")
        .WithEnvironment("RUSTFS_ACCESS_KEY", StorageUser)
        .WithEnvironment("RUSTFS_SECRET_KEY", StoragePassword)
        .WithPortBinding(S3Port, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(S3Port).ForPath("/health")))
        .Build();

    /// <summary>
    /// Clients talk https, like browsers do: the guest and sign-in cookies are Secure, and a client
    /// doesn't send those back over plain http.
    /// </summary>
    public ApiFactory() => ClientOptions.BaseAddress = new Uri("https://localhost");

    /// <summary>Starts both containers before the first test.</summary>
    public Task InitializeAsync() => Task.WhenAll(_postgres.StartAsync(), _storage.StartAsync());

    /// <summary>Points the API at the test containers.</summary>
    /// <param name="builder">The test host's builder.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development": that would also load the developer's user secrets into the tests.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:designapp", _postgres.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Storage:ServiceUrl", $"http://{_storage.Hostname}:{_storage.GetMappedPublicPort(S3Port)}");
        builder.UseSetting("Storage:AccessKey", StorageUser);
        builder.UseSetting("Storage:SecretKey", StoragePassword);
        builder.UseSetting("Storage:Bucket", "integration-tests");
        builder.UseSetting("Ai:UseFakes", "true"); // never call paid AI from tests
        builder.UseSetting("RateLimits:AccountAttemptsPer10Minutes", "1000"); // every test client shares one address
    }

    /// <summary>Stops the API, then removes the containers.</summary>
    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await _postgres.DisposeAsync();
        await _storage.DisposeAsync();
    }
}

/// <summary>
/// The API without its analysis worker, for tests that write rooms straight to the database: the
/// worker would otherwise pick up their unanalyzed rooms at startup and change them mid-test.
/// </summary>
public sealed class ApiFactoryWithoutAnalysis : ApiFactory
{
    /// <summary>Removes the analysis worker from the API's services.</summary>
    /// <param name="builder">The test host's builder.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            foreach (var worker in services.Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType?.Name == "RoomAnalysisWorker").ToList())
                services.Remove(worker);
        });
    }
}
