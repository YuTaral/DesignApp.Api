using DesignApp.AppHost;

// Aspire AppHost: the one project to run for local development. It starts Postgres, the photo
// storage, the API and the web app, wires them together and opens the Aspire dashboard.
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres(Constants.Resources.Postgres)
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);
var database = postgres.AddDatabase(Constants.Resources.Database);

// S3-compatible object storage for photos. RustFS locally; any S3 service (AWS, R2, B2) in the cloud.
// The password is generated once and kept in the AppHost's user secrets.
var storageUser = builder.AddParameter(Constants.Resources.StorageUser, Constants.Resources.StorageUserName);
var storagePassword = builder.AddParameter(Constants.Resources.StoragePassword,
    new GenerateParameterDefault { MinLength = Constants.Resources.StoragePasswordLength, Special = false }, secret: true, persist: true);
var storage = builder.AddContainer(Constants.Resources.Storage, Constants.Resources.StorageImage, Constants.Resources.StorageTag)
    .WithEnvironment(Constants.Resources.StorageAccessKeyVariable, storageUser)
    .WithEnvironment(Constants.Resources.StorageSecretKeyVariable, storagePassword)
    .WithHttpEndpoint(targetPort: Constants.Resources.S3Port, name: Constants.Resources.S3Endpoint)
    .WithHttpEndpoint(targetPort: Constants.Resources.ConsolePort, name: Constants.Resources.ConsoleEndpoint)
    .WithHttpHealthCheck(Constants.Resources.StorageHealthPath, endpointName: Constants.Resources.S3Endpoint)
    .WithVolume(Constants.Resources.StorageVolume, Constants.Resources.StorageDataPath)
    .WithLifetime(ContainerLifetime.Persistent);

var api = builder.AddProject<Projects.DesignApp_Api>(Constants.Resources.Api)
    .WithReference(database)
    .WaitFor(database)
    .WithEnvironment(Constants.Resources.StorageServiceUrlVariable, storage.GetEndpoint(Constants.Resources.S3Endpoint))
    .WithEnvironment(Constants.Resources.StorageAccessKeySetting, storageUser)
    .WithEnvironment(Constants.Resources.StorageSecretKeySetting, storagePassword)
    .WaitFor(storage)
    .WithExternalHttpEndpoints();

// The frontend is a separate repository. Clone DesignApp.Web next to DesignApp.Api
// (both inside one DesignApp folder) and the AppHost runs it. The reference gives the Vite
// dev server the API's address, and it forwards /api there, so the browser stays on one origin.
var webPath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "..", Constants.Resources.WebFolder));

if (Directory.Exists(webPath))
{
    // Always http://localhost:5173, so a browser tab left open from an earlier run still works
    // after a restart (a random port per run left old tabs pointing at nothing).
    var web = builder.AddViteApp(Constants.Resources.Web, webPath)
        .WithEndpoint(Constants.Resources.WebEndpoint, endpoint => endpoint.Port = Constants.Resources.WebPort)
        .WithReference(api)
        .WaitFor(api)
        .WithExternalHttpEndpoints();

    // Only needed if the web app is pointed straight at the API (VITE_API_URL). Index 1 keeps
    // the appsettings origin (index 0) allowed too.
    api.WithEnvironment(Constants.Resources.WebOriginVariable, web.GetEndpoint(Constants.Resources.WebEndpoint));
}

builder.Build().Run();
