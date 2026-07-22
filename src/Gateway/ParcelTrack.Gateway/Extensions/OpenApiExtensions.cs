using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace ParcelTrack.Gateway.Extensions;

public static class OpenApiExtensions
{
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, ct) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "ParcelTrack — API Gateway",
                    Version = "v1",
                    Description = "Reverse proxy that routes parcel-tracking requests to downstream services."
                };

                document.Components ??= new OpenApiComponents();
                document.Components?.SecuritySchemes?["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Get a token from POST /realms/parceltrack/protocol/openid-connect/token"
                };

                return Task.CompletedTask;
            });
        });

        return services;
    }

    public static IEndpointRouteBuilder MapApiDocumentation(this IEndpointRouteBuilder app)
    {
        app.MapOpenApi();

        app.MapScalarApiReference("/", options =>
        {
            options.Title = "ParcelTrack — API Gateway";
            options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
        });

        return app;
    }
}
