using Scalar.AspNetCore;

namespace DotNetCloud.Core.Server.Configuration;

/// <summary>
/// Extension methods for configuring OpenAPI documentation.
/// </summary>
public static class OpenApiConfiguration
{
    /// <summary>
    /// Adds DotNetCloud OpenAPI documentation services.
    /// Uses the built-in Microsoft.AspNetCore.OpenApi for schema generation
    /// and Scalar for the interactive API documentation UI.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDotNetCloudOpenApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Use the built-in .NET OpenAPI document generation
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, context, ct) =>
            {
                document.Info.Title = "DotNetCloud API";
                document.Info.Version = "1.0.0";
                document.Info.Description = """
                    DotNetCloud is a self-hosted, modular cloud platform.
                    This API provides access to core services including authentication,
                    user management, module management, and system administration.
                    """;
                return Task.CompletedTask;
            });
        });

        return services;
    }

    /// <summary>
    /// Configures the OpenAPI documentation middleware pipeline.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication UseDotNetCloudOpenApi(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference(options =>
            {
                options
                    .WithTitle("DotNetCloud API Documentation")
                    .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
            });
        }

        return app;
    }
}
