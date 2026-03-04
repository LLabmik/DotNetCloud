using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DotNetCloud.Core.ServiceDefaults.Telemetry;

/// <summary>
/// Configuration options for OpenTelemetry.
/// </summary>
public class TelemetryOptions
{
    /// <summary>
    /// Gets or sets the service name for telemetry.
    /// </summary>
    public string ServiceName { get; set; } = "DotNetCloud";

    /// <summary>
    /// Gets or sets the service version for telemetry.
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets whether to enable metrics collection.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable distributed tracing.
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// Gets or sets the OTLP exporter endpoint URL.
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// Gets or sets whether to export to console (for development).
    /// </summary>
    public bool EnableConsoleExporter { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to enable the Prometheus metrics scraping endpoint at <c>/metrics</c>.
    /// When enabled, <see cref="TelemetryConfigurationExtensions.MapDotNetCloudPrometheus"/> must
    /// also be called on the <see cref="WebApplication"/> to register the endpoint.
    /// </summary>
    public bool EnablePrometheusExporter { get; set; } = false;

    /// <summary>
    /// Gets or sets additional activity sources to trace.
    /// </summary>
    public List<string> AdditionalSources { get; set; } = new();

    /// <summary>
    /// Gets or sets additional meters to collect metrics from.
    /// </summary>
    public List<string> AdditionalMeters { get; set; } = new();
}

/// <summary>
/// Extension methods for configuring OpenTelemetry.
/// </summary>
public static class TelemetryConfigurationExtensions
{
    /// <summary>
    /// Configures OpenTelemetry with DotNetCloud defaults.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <param name="configureOptions">Optional action to configure telemetry options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDotNetCloudTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        Action<TelemetryOptions>? configureOptions = null)
    {
        var options = new TelemetryOptions();
        configuration.GetSection("Telemetry").Bind(options);
        configureOptions?.Invoke(options);

        // Configure resource
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: options.ServiceName,
                serviceVersion: options.ServiceVersion)
            .AddAttributes(new Dictionary<string, object>
            {
                ["environment"] = environment.EnvironmentName,
                ["host.name"] = Environment.MachineName
            });

        var otel = services.AddOpenTelemetry();

        // Configure metrics
        if (options.EnableMetrics)
        {
            otel.WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    // ASP.NET Core metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    // Built-in .NET meters
                    .AddMeter("Microsoft.AspNetCore.Hosting")
                    .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
                    .AddMeter("Microsoft.AspNetCore.Http.Connections")
                    .AddMeter("Microsoft.AspNetCore.Routing")
                    .AddMeter("System.Net.Http")
                    .AddMeter("System.Net.NameResolution");

                // Add custom meters
                foreach (var meter in options.AdditionalMeters)
                {
                    metrics.AddMeter(meter);
                }

                // Configure exporters
                ConfigureMetricsExporters(metrics, options, environment);
            });
        }

        // Configure tracing
        if (options.EnableTracing)
        {
            otel.WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .SetSampler(environment.IsDevelopment()
                        ? new AlwaysOnSampler()
                        : new ParentBasedSampler(new TraceIdRatioBasedSampler(0.1)))
                    // ASP.NET Core instrumentation
                    .AddAspNetCoreInstrumentation(opts =>
                    {
                        opts.RecordException = true;
                        opts.Filter = httpContext =>
                        {
                            // Don't trace health check endpoints
                            return !httpContext.Request.Path.StartsWithSegments("/health");
                        };
                    })
                    .AddHttpClientInstrumentation(opts =>
                    {
                        opts.RecordException = true;
                    })
                    // gRPC instrumentation
                    .AddGrpcClientInstrumentation()
                    // Built-in sources
                    .AddSource("Microsoft.AspNetCore.Hosting")
                    .AddSource("Microsoft.AspNetCore.Server.Kestrel")
                    .AddSource("Microsoft.AspNetCore.Http.Connections")
                    .AddSource("Microsoft.AspNetCore.Routing")
                    .AddSource("System.Net.Http");

                // Add custom sources
                foreach (var source in options.AdditionalSources)
                {
                    tracing.AddSource(source);
                }

                // Configure exporters
                ConfigureTracingExporters(tracing, options, environment);
            });
        }

        return services;
    }

    private static void ConfigureMetricsExporters(
        MeterProviderBuilder metrics,
        TelemetryOptions options,
        IHostEnvironment environment)
    {
        // Console exporter for development
        if (options.EnableConsoleExporter || environment.IsDevelopment())
        {
            metrics.AddConsoleExporter();
        }

        // OTLP exporter for production
        if (!string.IsNullOrEmpty(options.OtlpEndpoint))
        {
            metrics.AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(options.OtlpEndpoint);
            });
        }

        // Prometheus scraping endpoint
        if (options.EnablePrometheusExporter)
        {
            metrics.AddPrometheusExporter();
        }
    }

    private static void ConfigureTracingExporters(
        TracerProviderBuilder tracing,
        TelemetryOptions options,
        IHostEnvironment environment)
    {
        // Console exporter for development
        if (options.EnableConsoleExporter || environment.IsDevelopment())
        {
            tracing.AddConsoleExporter();
        }

        // OTLP exporter for production
        if (!string.IsNullOrEmpty(options.OtlpEndpoint))
        {
            tracing.AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(options.OtlpEndpoint);
            });
        }
    }

    /// <summary>
    /// Maps the Prometheus metrics scraping endpoint at <c>/metrics</c>.
    /// Only has effect when <see cref="TelemetryOptions.EnablePrometheusExporter"/> is <c>true</c>.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication MapDotNetCloudPrometheus(this WebApplication app)
    {
        var options = new TelemetryOptions();
        app.Configuration.GetSection("Telemetry").Bind(options);

        if (options.EnablePrometheusExporter)
        {
            app.MapPrometheusScrapingEndpoint();
        }

        return app;
    }
}

/// <summary>
/// Constants for telemetry activity sources.
/// </summary>
public static class TelemetryActivitySources
{
    /// <summary>
    /// Activity source for core operations.
    /// </summary>
    public static readonly ActivitySource Core = new("DotNetCloud.Core");

    /// <summary>
    /// Activity source for module operations.
    /// </summary>
    public static readonly ActivitySource Modules = new("DotNetCloud.Modules");

    /// <summary>
    /// Activity source for authentication operations.
    /// </summary>
    public static readonly ActivitySource Authentication = new("DotNetCloud.Authentication");

    /// <summary>
    /// Activity source for authorization operations.
    /// </summary>
    public static readonly ActivitySource Authorization = new("DotNetCloud.Authorization");
}
