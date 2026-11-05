using DesignApp.Application.Analysis;
using DesignApp.Application.Rooms;
using Microsoft.Extensions.DependencyInjection;

namespace DesignApp.Application;

/// <summary>Registers the Application layer's services.</summary>
public static class DependencyInjection
{
    /// <summary>Adds the use cases (such as <see cref="UploadRoom"/>) and the system clock.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<UploadRoom>();
        services.AddScoped<GetRoom>();
        services.AddScoped<AnalyzeRoom>();
        services.AddScoped<FixRoomOutline>();
        return services;
    }
}
