using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace DesignApp.IntegrationTests;

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
