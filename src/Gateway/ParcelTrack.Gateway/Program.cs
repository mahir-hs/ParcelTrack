using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using ParcelTrack.Gateway.Extensions;

var builder = WebApplication.CreateBuilder(args);

// YARP reverse proxy — routes are loaded from the "ReverseProxy" config section.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHealthChecks();
builder.Services.AddApiDocumentation();

var app = builder.Build();

app.MapHealthChecks("/health");

app.MapReverseProxy();

if (app.Environment.IsDevelopment())
{
    app.MapApiDocumentation();
}

app.Run();
