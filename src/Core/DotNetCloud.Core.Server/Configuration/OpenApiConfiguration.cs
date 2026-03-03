namespace DotNetCloud.Core.Server.Configuration;

/// <summary>
/// Extension methods for configuring OpenAPI/Swagger documentation.
/// </summary>
public static class OpenApiConfiguration
{
    /// <summary>
    /// Adds DotNetCloud OpenAPI documentation services.
    /// Uses the built-in Microsoft.AspNetCore.OpenApi for schema generation
    /// and Swashbuckle for the Swagger UI.
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
    /// Configures the OpenAPI/Swagger UI middleware pipeline.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication UseDotNetCloudOpenApi(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();

            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/openapi/v1.json", "DotNetCloud API v1");
                options.DocumentTitle = "DotNetCloud API Documentation";
                options.RoutePrefix = "swagger";
                options.DefaultModelsExpandDepth(1);
                options.DisplayRequestDuration();
                options.EnableDeepLinking();
                options.EnableFilter();
                options.ShowExtensions();
            });
        }

        return app;
    }
}
