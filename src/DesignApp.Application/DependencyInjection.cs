using DesignApp.Application.Rooms;
using Microsoft.Extensions.DependencyInjection;

namespace DesignApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<UploadRoom>();
        services.AddScoped<GetRoom>();
        return services;
    }
}
