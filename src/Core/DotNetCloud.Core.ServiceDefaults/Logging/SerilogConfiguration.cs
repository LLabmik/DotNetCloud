using Serilog;
using Serilog.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace DotNetCloud.Core.ServiceDefaults.Logging;

/// <summary>
/// Configuration options for Serilog logging.
/// </summary>
public class SerilogOptions
{
    /// <summary>
    /// Gets or sets the minimum log level for console output.
    /// </summary>
    public LogEventLevel ConsoleMinimumLevel { get; set; } = LogEventLevel.Information;

    /// <summary>
    /// Gets or sets the minimum log level for file output.
    /// </summary>
    public LogEventLevel FileMinimumLevel { get; set; } = LogEventLevel.Warning;

    /// <summary>
    /// Gets or sets the path for log files.
    /// </summary>
    public string FilePath { get; set; } = "logs/dotnetcloud-.log";

    /// <summary>
    /// Gets or sets whether to roll log files daily.
    /// </summary>
    public bool RollingDaily { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of days to retain log files.
    /// </summary>
    public int RetainedFileCountLimit { get; set; } = 31;

    /// <summary>
    /// Gets or sets the maximum log file size in bytes (null for unlimited).
    /// </summary>
    public long? FileSizeLimitBytes { get; set; } = 100 * 1024 * 1024; // 100 MB

    /// <summary>
    /// Gets or sets whether to enable structured logging format.
    /// </summary>
    public bool UseStructuredFormat { get; set; } = true;

    /// <summary>
    /// Gets or sets the path for the audit log file. Null disables the dedicated audit sink.
    /// </summary>
    public string? AuditFilePath { get; set; } = "logs/audit-sync-.log";

    /// <summary>
    /// Gets or sets module names to exclude from logging.
    /// </summary>
    public HashSet<string> ExcludedModules { get; set; } = new();

    /// <summary>
    /// Gets or sets module-specific log levels.
    /// </summary>
    public Dictionary<string, LogEventLevel> ModuleLogLevels { get; set; } = new();
}

/// <summary>
/// Extension methods for configuring Serilog.
/// </summary>
public static class SerilogConfigurationExtensions
{
    private const string OutputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    /// <summary>
    /// Configures Serilog with DotNetCloud defaults.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <param name="configureOptions">Optional action to configure Serilog options.</param>
    /// <returns>The host builder for chaining.</returns>
    public static IHostBuilder UseDotNetCloudSerilog(
        this IHostBuilder builder,
        Action<SerilogOptions>? configureOptions = null)
    {
        return builder.UseSerilog((context, services, configuration) =>
        {
            var options = new SerilogOptions();
            context.Configuration.GetSection("Serilog").Bind(options);
            configureOptions?.Invoke(options);

            ConfigureSerilog(configuration, context.HostingEnvironment, options);
        });
    }

    /// <summary>
    /// Configures Serilog with DotNetCloud defaults.
    /// </summary>
    /// <param name="configuration">The logger configuration.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <param name="options">Serilog options.</param>
    public static void ConfigureSerilog(
        LoggerConfiguration configuration,
        IHostEnvironment environment,
        SerilogOptions options)
    {
        configuration
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning);

        // Add module-specific log levels
        foreach (var (moduleName, level) in options.ModuleLogLevels)
        {
            configuration.MinimumLevel.Override(moduleName, level);
        }

        // Apply module filter
        if (options.ExcludedModules.Any() || options.ModuleLogLevels.Any())
        {
            configuration.Filter.With(new ModuleLogFilter(
                options.ExcludedModules,
                options.ModuleLogLevels));
        }

        // Console sink
        if (environment.IsDevelopment())
        {
            configuration.WriteTo.Console(
                restrictedToMinimumLevel: options.ConsoleMinimumLevel,
                outputTemplate: OutputTemplate);
        }
        else
        {
            configuration.WriteTo.Console(
                restrictedToMinimumLevel: options.ConsoleMinimumLevel,
                outputTemplate: options.UseStructuredFormat
                    ? OutputTemplate
                    : "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        }

        // File sink
        configuration.WriteTo.File(
            path: options.FilePath,
            restrictedToMinimumLevel: options.FileMinimumLevel,
            rollingInterval: options.RollingDaily ? RollingInterval.Day : RollingInterval.Infinite,
            retainedFileCountLimit: options.RetainedFileCountLimit,
            fileSizeLimitBytes: options.FileSizeLimitBytes,
            outputTemplate: OutputTemplate,
            shared: true,
            flushToDiskInterval: TimeSpan.FromSeconds(1));

        // Audit log sink — captures file and sync operations
        if (!string.IsNullOrEmpty(options.AuditFilePath))
        {
            configuration.WriteTo.Logger(lc => lc
                .Filter.ByIncludingOnly(e =>
                    e.MessageTemplate.Text.StartsWith("file.", StringComparison.Ordinal) ||
                    e.MessageTemplate.Text.StartsWith("sync.", StringComparison.Ordinal))
                .WriteTo.File(
                    path: options.AuditFilePath,
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    rollingInterval: options.RollingDaily ? RollingInterval.Day : RollingInterval.Infinite,
                    retainedFileCountLimit: options.RetainedFileCountLimit,
                    fileSizeLimitBytes: options.FileSizeLimitBytes,
                    outputTemplate: OutputTemplate,
                    shared: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(1)));
        }
    }
}
