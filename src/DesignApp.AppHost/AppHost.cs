var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);
var database = postgres.AddDatabase("designapp");

// S3-compatible object storage for photos. RustFS locally; any S3 service (AWS, R2, B2) in the cloud.
// The password is generated once and kept in the AppHost's user secrets.
var storageUser = builder.AddParameter("storage-user", "designapp");
var storagePassword = builder.AddParameter("storage-password",
    new GenerateParameterDefault { MinLength = 32, Special = false }, secret: true, persist: true);
var storage = builder.AddContainer("storage", "rustfs/rustfs", "1.0.4")
    .WithEnvironment("RUSTFS_ACCESS_KEY", storageUser)
    .WithEnvironment("RUSTFS_SECRET_KEY", storagePassword)
    .WithHttpEndpoint(targetPort: 9000, name: "s3")
    .WithHttpEndpoint(targetPort: 9001, name: "console")
    .WithHttpHealthCheck("/health", endpointName: "s3")
    .WithVolume("designapp-storage", "/data")
    .WithLifetime(ContainerLifetime.Persistent);

var api = builder.AddProject<Projects.DesignApp_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithEnvironment("Storage__ServiceUrl", storage.GetEndpoint("s3"))
    .WithEnvironment("Storage__AccessKey", storageUser)
    .WithEnvironment("Storage__SecretKey", storagePassword)
    .WaitFor(storage)
    .WithExternalHttpEndpoints();

// The frontend is a separate repository. Clone DesignApp.Web next to DesignApp.Api
// (both inside one DesignApp folder) and the AppHost runs it. The reference gives the Vite
// dev server the API's address, and it forwards /api there, so the browser stays on one origin.
var webPath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "..", "DesignApp.Web"));
if (Directory.Exists(webPath))
{
    var web = builder.AddViteApp("web", webPath)
        .WithReference(api)
        .WaitFor(api)
        .WithExternalHttpEndpoints();

    // Only needed if the web app is pointed straight at the API (VITE_API_URL). Aspire picks the
    // web port at run time; index 1 keeps the appsettings origin (index 0) allowed too.
    api.WithEnvironment("Cors__WebOrigins__1", web.GetEndpoint("http"));
}

builder.Build().Run();
