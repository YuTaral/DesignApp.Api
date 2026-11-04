using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace DesignApp.IntegrationTests;

/// <summary>
/// Runs the real API against real Postgres and real S3-compatible storage in throwaway containers,
/// so the tests cover the SQL and S3 calls rather than in-memory stand-ins.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
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

    public Task InitializeAsync() => Task.WhenAll(_postgres.StartAsync(), _storage.StartAsync());

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
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await _postgres.DisposeAsync();
        await _storage.DisposeAsync();
    }
}
