using System.Text.Json.Serialization;
using DesignApp.Api;
using DesignApp.Api.Accounts;
using DesignApp.Api.Designs;
using DesignApp.Api.Rooms;
using DesignApp.Application;
using DesignApp.Application.Analysis;
using DesignApp.Application.Abstractions;
using DesignApp.Application.Photos;
using DesignApp.Infrastructure;
using Scalar.AspNetCore;

// Entry point of the HTTP API: wires up services, middleware and the /api endpoints.
var builder = WebApplication.CreateBuilder(args);

// The API needs the database and storage that the AppHost starts. Fail with directions rather
// than a connection-string error when someone starts this project on its own.
if (string.IsNullOrEmpty(builder.Configuration.GetConnectionString(DesignApp.Infrastructure.DependencyInjection.DatabaseName)))
{
    throw new InvalidOperationException(
        "DesignApp.Api can't run on its own: it needs the database and storage that DesignApp.AppHost starts. " +
        "Run DesignApp.AppHost instead (in Visual Studio: right-click DesignApp.AppHost > Set as Startup Project, then F5).");
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
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = PhotoRules.MaxUploadBytes + 1024 * 1024);

const string WebCorsPolicy = "web";
var webOrigins = builder.Configuration.GetSection("Cors:WebOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(WebCorsPolicy, policy => policy
    .WithOrigins(webOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .WithExposedHeaders("Location")));

var app = builder.Build();

await app.Services.InitializeInfrastructureAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("DesignApp API"));
}

app.UseHttpsRedirection();
app.UseCors(WebCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapDefaultEndpoints();

var api = app.MapGroup("/api");

api.MapGet("/status", (IHostEnvironment env) => new ApiStatus("DesignApp API", env.EnvironmentName))
    .WithName("GetStatus")
    .WithSummary("Reports that the API is up and which environment it runs in.");

api.MapAccountEndpoints();
api.MapDesignEndpoints();
api.MapRoomEndpoints();
app.MapHub<RoomHub>(RoomHub.Path);

app.Run();

/// <summary>Body of <c>GET /api/status</c>.</summary>
/// <param name="Name">The service's display name.</param>
/// <param name="Environment">The hosting environment, such as "Development".</param>
internal sealed record ApiStatus(string Name, string Environment);
