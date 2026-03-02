using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DotNetCloud.Core.ServiceDefaults.HealthChecks;
using DotNetCloud.Core.ServiceDefaults.Logging;
using DotNetCloud.Core.ServiceDefaults.Middleware;
using DotNetCloud.Core.ServiceDefaults.Telemetry;

namespace DotNetCloud.Core.ServiceDefaults.Extensions;

/// <summary>
/// Extension methods for configuring DotNetCloud service defaults.
/// </summary>
public static class ServiceDefaultsExtensions
{
    /// <summary>
    /// Adds DotNetCloud service defaults to the host builder.
    /// This includes logging, telemetry, health checks, and common middleware.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configureSerilog">Optional action to configure Serilog.</param>
    /// <param name="configureTelemetry">Optional action to configure OpenTelemetry.</param>
    /// <returns>The host application builder for chaining.</returns>
    public static IHostApplicationBuilder AddDotNetCloudServiceDefaults(
        this IHostApplicationBuilder builder,
        Action<SerilogOptions>? configureSerilog = null,
        Action<TelemetryOptions>? configureTelemetry = null)
    {
        // Add logging
        builder.Services.AddLogging();

        // Add telemetry
        builder.Services.AddDotNetCloudTelemetry(
            builder.Configuration,
            builder.Environment,
            configureTelemetry);

        // Add health checks
        builder.Services.AddHealthChecks();

        return builder;
    }

    /// <summary>
    /// Adds DotNetCloud service defaults to the web application builder.
    /// This includes logging, telemetry, health checks, and common middleware.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="configureSerilog">Optional action to configure Serilog.</param>
    /// <param name="configureTelemetry">Optional action to configure OpenTelemetry.</param>
    /// <returns>The web application builder for chaining.</returns>
    public static WebApplicationBuilder AddDotNetCloudServiceDefaults(
        this WebApplicationBuilder builder,
        Action<SerilogOptions>? configureSerilog = null,
        Action<TelemetryOptions>? configureTelemetry = null)
    {
        // Configure Serilog
        builder.Host.UseDotNetCloudSerilog(configureSerilog);

        // Add telemetry
        builder.Services.AddDotNetCloudTelemetry(
            builder.Configuration,
            builder.Environment,
            configureTelemetry);

        // Add health checks
        builder.Services.AddHealthChecks();

        // Add CORS
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                var allowedOrigins = builder.Configuration
                    .GetSection("Cors:AllowedOrigins")
                    .Get<string[]>() ?? Array.Empty<string>();

                if (allowedOrigins.Any())
                {
                    policy.WithOrigins(allowedOrigins);
                }
                else
                {
                    policy.AllowAnyOrigin();
                }

                policy.AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });
        });

        return builder;
    }

    /// <summary>
    /// Uses DotNetCloud middleware defaults for the web application.
    /// This includes security headers, exception handling, and request/response logging.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="configureSecurityHeaders">Optional action to configure security headers.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication UseDotNetCloudMiddleware(
        this WebApplication app,
        Action<SecurityHeadersOptions>? configureSecurityHeaders = null)
    {
        // Security headers
        var securityHeadersOptions = new SecurityHeadersOptions();
        configureSecurityHeaders?.Invoke(securityHeadersOptions);
        app.UseMiddleware<SecurityHeadersMiddleware>(securityHeadersOptions);

        // Exception handling
        app.UseMiddleware<GlobalExceptionHandlerMiddleware>(app.Environment.IsDevelopment());

        // Request/Response logging
        if (app.Environment.IsDevelopment())
        {
            app.UseMiddleware<RequestResponseLoggingMiddleware>();
        }

        // CORS
        app.UseCors();

        // HTTPS redirection
        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        return app;
    }

    /// <summary>
    /// Maps health check endpoints for the web application.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication MapDotNetCloudHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/ready");
        app.MapHealthChecks("/health/live");

        return app;
    }

    /// <summary>
    /// Registers a module health check.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="moduleHealthCheck">The module health check instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddModuleHealthCheck(
        this IServiceCollection services,
        IModuleHealthCheck moduleHealthCheck)
    {
        services.AddHealthChecks()
            .Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration(
                name: $"module-{moduleHealthCheck.ModuleName}",
                instance: new ModuleHealthCheckAdapter(moduleHealthCheck),
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                tags: new[] { "module", moduleHealthCheck.ModuleName }));

        return services;
    }

    /// <summary>
    /// Registers a database health check.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The name of the health check.</param>
    /// <param name="tags">Optional tags for the health check.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDatabaseHealthCheck(
        this IServiceCollection services,
        string name = "database",
        params string[] tags)
    {
        services.AddHealthChecks()
            .Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration(
                name: name,
                factory: sp => new DatabaseHealthCheck(sp.GetRequiredService<IDbConnectionFactory>()),
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                tags: tags.Any() ? tags : new[] { "database", "core" }));

        return services;
    }
}
