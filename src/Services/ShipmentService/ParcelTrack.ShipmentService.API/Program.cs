using Microsoft.EntityFrameworkCore;
using ParcelTrack.ShipmentService.API;
using ParcelTrack.ShipmentService.API.Extensions;
using ParcelTrack.ShipmentService.Application;
using ParcelTrack.ShipmentService.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration, builder.Environment);

var app = builder.Build();

await app.UseApiPipelineAsync();

await app.RunAsync();

public partial class Program { }