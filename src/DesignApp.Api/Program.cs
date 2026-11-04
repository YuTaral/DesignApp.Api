using System.Text.Json.Serialization;
using DesignApp.Api;
using DesignApp.Api.Rooms;
using DesignApp.Application;
using DesignApp.Application.Abstractions;
using DesignApp.Application.Photos;
using DesignApp.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddInfrastructure();
builder.Services.AddApplication();
builder.Services.AddScoped<ICurrentUser, GuestUser>();

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
app.UseRateLimiter();

app.MapDefaultEndpoints();

var api = app.MapGroup("/api");

api.MapGet("/status", (IHostEnvironment env) => new ApiStatus("DesignApp API", env.EnvironmentName))
    .WithName("GetStatus")
    .WithSummary("Reports that the API is up and which environment it runs in.");

api.MapRoomEndpoints();

app.Run();

internal sealed record ApiStatus(string Name, string Environment);
