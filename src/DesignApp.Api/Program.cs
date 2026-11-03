using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

const string WebCorsPolicy = "web";
var webOrigins = builder.Configuration.GetSection("Cors:WebOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(WebCorsPolicy, policy => policy
    .WithOrigins(webOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("DesignApp API"));
}

app.UseHttpsRedirection();
app.UseCors(WebCorsPolicy);

app.MapDefaultEndpoints();

var api = app.MapGroup("/api");

api.MapGet("/status", (IHostEnvironment env) => new ApiStatus("DesignApp API", env.EnvironmentName))
    .WithName("GetStatus")
    .WithSummary("Reports that the API is up and which environment it runs in.");

app.Run();

internal sealed record ApiStatus(string Name, string Environment);
