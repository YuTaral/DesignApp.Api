using System.Text.Json.Serialization;
using DesignApp.Api.Endpoints;
using DesignApp.Api.Hubs;
using DesignApp.Api.Middleware;
using DesignApp.Api.Responses;
using DesignApp.Api.Services;
using DesignApp.Api.Setup;
using DesignApp.Application;
using DesignApp.Application.Interfaces;
using DesignApp.Infrastructure;
using Scalar.AspNetCore;

// Entry point of the HTTP API: wires up services, middleware and the /api endpoints.
var builder = WebApplication.CreateBuilder(args);

// The API needs the database and storage that the AppHost starts. Fail with directions rather
// than a connection-string error when someone starts this project on its own.
if (string.IsNullOrEmpty(builder.Configuration.GetConnectionString(DesignApp.Infrastructure.Constants.Config.DatabaseName)))
{
    throw new InvalidOperationException(DesignApp.Api.Constants.Messages.NeedsAppHost);
}

builder.AddServiceDefaults();
builder.AddInfrastructure();
builder.Services.AddApplication();
builder.Services.AddDesignAppAccounts();
builder.Services.AddSingleton<IRoomNotifier, SignalRRoomNotifier>();
builder.Services.AddSignalR().AddJsonProtocol(options =>
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<UserErrorExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddDesignAppRateLimits(builder.Configuration);

// Room for the photo plus the multipart framing; the endpoint enforces the exact photo limit.
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = DesignApp.Application.Constants.Photos.MaxUploadBytes + DesignApp.Api.Constants.Http.UploadFramingBytes);

var webOrigins = builder.Configuration.GetSection(DesignApp.Api.Constants.Http.WebOriginsKey).Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(DesignApp.Api.Constants.Http.WebCorsPolicy, policy => policy
    .WithOrigins(webOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .WithExposedHeaders(DesignApp.Api.Constants.Http.Location)));

var app = builder.Build();

await app.Services.InitializeInfrastructureAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle(DesignApp.Api.Constants.OpenApi.Title));
}

app.UseHttpsRedirection();
app.UseCors(DesignApp.Api.Constants.Http.WebCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapDefaultEndpoints();

// A request the browser abandoned ends quietly instead of as an error (see ClientDisconnectedFilter).
var api = app.MapGroup(DesignApp.Api.Constants.Routes.Api).AddEndpointFilter<ClientDisconnectedFilter>();

api.MapGet(DesignApp.Api.Constants.Routes.Status, (IHostEnvironment env) => new ApiStatus(DesignApp.Api.Constants.OpenApi.Title, env.EnvironmentName))
    .WithName("GetStatus")
    .WithSummary("Reports that the API is up and which environment it runs in.");

api.MapAccountEndpoints();
api.MapDesignEndpoints();
api.MapRoomEndpoints();
api.MapLibraryEndpoints();
if (app.Environment.IsDevelopment())
    api.MapDevEndpoints();
app.MapHub<RoomHub>(DesignApp.Api.Constants.Hub.Path);

app.Run();
