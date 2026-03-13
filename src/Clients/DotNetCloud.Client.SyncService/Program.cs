using DotNetCloud.Client.SyncService;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(args);
var loggingSettings = LoadLoggingSettings();
var logPath = BuildLogPath();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(loggingSettings.MinimumLevel)
    .WriteTo.File(
        new CompactJsonFormatter(),
        logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: loggingSettings.RetentionDays,
        fileSizeLimitBytes: loggingSettings.MaxFileSizeMB * 1024L * 1024L,
        rollOnFileSizeLimit: true,
        shared: true)
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger, dispose: true);

// Register as a Windows Service (no-op on Linux)
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "DotNetCloud Sync Service";
});

// Register systemd integration (no-op on Windows)
builder.Services.AddSystemd();

builder.Services.AddDotNetCloudSyncService();

var app = builder.Build();

try
{
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "DotNetCloud Sync Service terminated unexpectedly.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static SyncLoggingSettings LoadLoggingSettings()
{
    var defaults = new SyncLoggingSettings
    {
        RetentionDays = 30,
        MaxFileSizeMB = 50,
        MinimumLevel = LogEventLevel.Information,
    };

    var candidates = new[]
    {
        Path.Combine(AppContext.BaseDirectory, "sync-settings.json"),
        Path.Combine(GetSystemDataRoot(), "sync-settings.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "sync-settings.json"),
    };

    var settingsPath = candidates.FirstOrDefault(File.Exists);
    if (settingsPath is null)
    {
        return defaults;
    }

    try
    {
        using var stream = File.OpenRead(settingsPath);
        using var document = JsonDocument.Parse(stream);

        if (!document.RootElement.TryGetProperty("logging", out var loggingSection))
        {
            return defaults;
        }

        var retentionDays = loggingSection.TryGetProperty("retentionDays", out var retentionElement)
            && retentionElement.TryGetInt32(out var configuredRetention)
            && configuredRetention > 0
            ? configuredRetention
            : defaults.RetentionDays;

        var maxFileSizeMb = loggingSection.TryGetProperty("maxFileSizeMB", out var maxFileSizeElement)
            && maxFileSizeElement.TryGetInt32(out var configuredSize)
            && configuredSize > 0
            ? configuredSize
            : defaults.MaxFileSizeMB;

        var minimumLevel = loggingSection.TryGetProperty("minimumLevel", out var minimumLevelElement)
            && Enum.TryParse<LogEventLevel>(minimumLevelElement.GetString(), ignoreCase: true, out var configuredLevel)
            ? configuredLevel
            : defaults.MinimumLevel;

        return new SyncLoggingSettings
        {
            RetentionDays = retentionDays,
            MaxFileSizeMB = maxFileSizeMb,
            MinimumLevel = minimumLevel,
        };
    }
    catch
    {
        return defaults;
    }
}

static string BuildLogPath()
{
    var logDirectory = Path.Combine(GetSystemDataRoot(), "logs");
    Directory.CreateDirectory(logDirectory);
    var logPath = Path.Combine(logDirectory, "sync-service.log");

    if (!File.Exists(logPath))
    {
        using var _ = File.Create(logPath);
    }

    if (OperatingSystem.IsLinux())
    {
        File.SetUnixFileMode(logPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    return logPath;
}

static string GetSystemDataRoot() =>
    OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DotNetCloud", "Sync")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "DotNetCloud");

sealed class SyncLoggingSettings
{
    public int RetentionDays { get; init; }

    public int MaxFileSizeMB { get; init; }

    public LogEventLevel MinimumLevel { get; init; }
}
