using DesignApp.Application.UseCases.Design;
using DesignApp.Application.UseCases.Room;
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
        services.AddScoped<SaveGuestRooms>();
        services.AddScoped<CreateDesign>();
        services.AddScoped<EditDesignDetails>();
        services.AddScoped<AddRoomToDesign>();
        services.AddScoped<SaveRoomColors>();
        services.AddScoped<RemoveRoomFromDesign>();
        services.AddScoped<GetDesigns>();
        services.AddScoped<DeleteDesign>();
        services.AddScoped<RoomRemover>();
        services.AddScoped<DeleteUnsavedRooms>();
        return services;
    }
}
