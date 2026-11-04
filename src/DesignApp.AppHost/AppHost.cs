var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);
var database = postgres.AddDatabase("designapp");

var api = builder.AddProject<Projects.DesignApp_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithExternalHttpEndpoints();

// The frontend is a separate repository. Clone DesignApp.Web next to DesignApp.Api
// (both inside one DesignApp folder) and the AppHost runs it with the API URL wired in.
var webPath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "..", "DesignApp.Web"));
if (Directory.Exists(webPath))
{
    var web = builder.AddViteApp("web", webPath)
        .WithEnvironment("VITE_API_URL", api.GetEndpoint("https"))
        .WithReference(api)
        .WaitFor(api)
        .WithExternalHttpEndpoints();

    // Aspire picks the web port at run time, so the API learns its CORS origin from here.
    // Index 1 keeps the appsettings origin (index 0, used by a plain `npm run dev`) allowed.
    api.WithEnvironment("Cors__WebOrigins__1", web.GetEndpoint("http"));
}

builder.Build().Run();
