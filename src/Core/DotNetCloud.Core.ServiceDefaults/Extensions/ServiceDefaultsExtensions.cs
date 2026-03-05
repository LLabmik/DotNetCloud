using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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

        // Add health checks with startup probe
        var startupCheck = new StartupHealthCheck();
        builder.Services.AddSingleton(startupCheck);
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("Application is running."),
                tags: ["live"])
            .AddCheck<StartupHealthCheck>("startup",
                tags: ["ready"]);

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

        // Add health checks with startup probe
        var startupCheck = new StartupHealthCheck();
        builder.Services.AddSingleton(startupCheck);
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("Application is running."),
                tags: ["live"])
            .AddCheck<StartupHealthCheck>("startup",
                tags: ["ready"]);

        // Add CORS
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                var allowedOrigins = builder.Configuration
                    .GetSection("Cors:AllowedOrigins")
                    .Get<string[]>() ?? Array.Empty<string>();

                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins)
                          .AllowCredentials();
                }
                else
                {
                    policy.AllowAnyOrigin();
                }

                policy.AllowAnyMethod()
                      .AllowAnyHeader();
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

    private static readonly JsonSerializerOptions HealthJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Maps health check endpoints for the web application.
    /// <list type="bullet">
    /// <item><description><c>/health</c> — full status including all registered checks (modules, DB, etc.)</description></item>
    /// <item><description><c>/health/live</c> — liveness probe; only self-diagnostic checks (no external dependencies)</description></item>
    /// <item><description><c>/health/ready</c> — readiness probe; includes startup and dependency checks (DB, modules)</description></item>
    /// </list>
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication MapDotNetCloudHealthChecks(this WebApplication app)
    {
        // Full health report (all checks)
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteHealthReportAsync
        });

        // Liveness probe — app is running (no external deps)
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
            ResponseWriter = WriteHealthReportAsync
        });

        // Readiness probe — app + dependencies are ready
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready") || check.Tags.Contains("database") || check.Tags.Contains("module"),
            ResponseWriter = WriteHealthReportAsync
        });

        // Mark the application as ready after startup
        var startupCheck = app.Services.GetService<StartupHealthCheck>();
        startupCheck?.MarkReady();

        return app;
    }

    /// <summary>
    /// Writes a JSON health report response with individual entry details.
    /// </summary>
    private static async Task WriteHealthReportAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var entries = new Dictionary<string, object>();
        foreach (var (name, entry) in report.Entries)
        {
            entries[name] = new
            {
                status = entry.Status.ToString(),
                description = entry.Description,
                duration = entry.Duration.TotalMilliseconds,
                exception = entry.Exception?.Message,
                data = entry.Data.Count > 0 ? entry.Data : null
            };
        }

        var result = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            entries
        };

        await JsonSerializer.SerializeAsync(context.Response.Body, result, HealthJsonOptions);
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
            .Add(new HealthCheckRegistration(
                name: $"module-{moduleHealthCheck.ModuleName}",
                instance: new ModuleHealthCheckAdapter(moduleHealthCheck),
                failureStatus: HealthStatus.Unhealthy,
                tags: ["module", moduleHealthCheck.ModuleName]));

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
            .Add(new HealthCheckRegistration(
                name: name,
                factory: sp => new DatabaseHealthCheck(sp.GetRequiredService<IDbConnectionFactory>()),
                failureStatus: HealthStatus.Unhealthy,
                tags: tags.Length > 0 ? tags : ["database", "core"]));

        return services;
    }
}
