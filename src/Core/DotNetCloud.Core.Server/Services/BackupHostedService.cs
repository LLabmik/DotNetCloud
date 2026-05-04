using System.Diagnostics;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Background service that reads backup configuration from system settings and
/// executes scheduled backups at the configured time interval.
/// </summary>
public sealed class BackupHostedService : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(5);
    private const string Module = "dotnetcloud.core";
    private const string ServiceName = "Backup Hosted Service";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackgroundServiceTracker _tracker;
    private readonly ILogger<BackupHostedService> _logger;

    private DateTime? _lastScheduledBackupDate;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackupHostedService"/> class.
    /// </summary>
    public BackupHostedService(
        IServiceScopeFactory scopeFactory,
        IBackgroundServiceTracker tracker,
        ILogger<BackupHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackupHostedService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndRunScheduledBackupAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BackupHostedService polling loop.");
            }

            try
            {
                await Task.Delay(PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("BackupHostedService stopped.");
    }

    private async Task CheckAndRunScheduledBackupAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<IAdminSettingsService>();
        var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();

        // Read backup settings
        var settings = await settingsService.ListSettingsAsync(Module);

        var enabled = GetBoolSetting(settings, "Backup:Enabled");
        if (!enabled)
            return;

        var scheduleStr = GetStringSetting(settings, "Backup:Schedule", "daily");
        var timeStr = GetStringSetting(settings, "Backup:TimeUtc", "02:00");
        var dayOfWeekStr = GetStringSetting(settings, "Backup:DayOfWeek", "Sunday");

        // Parse the scheduled time
        if (!TimeOnly.TryParse(timeStr, out var scheduledTime))
            scheduledTime = new TimeOnly(2, 0); // default 02:00 UTC

        var now = DateTime.UtcNow;
        var todayScheduled = now.Date.Add(scheduledTime.ToTimeSpan());

        // Determine if we should run now
        var shouldRun = scheduleStr.ToLowerInvariant() switch
        {
            "daily" => now >= todayScheduled && _lastScheduledBackupDate != now.Date,
            "weekly" => now.DayOfWeek.ToString().Equals(dayOfWeekStr, StringComparison.OrdinalIgnoreCase)
                        && now >= todayScheduled
                        && _lastScheduledBackupDate != now.Date,
            "manual" => false,
            _ => false,
        };

        if (!shouldRun)
            return;

        _lastScheduledBackupDate = now.Date;

        _logger.LogInformation("Starting scheduled backup (schedule: {Schedule}, time: {Time} UTC).",
            scheduleStr, timeStr);

        var sw = Stopwatch.StartNew();
        var options = BuildBackupOptionsFromSettings(settings);

        try
        {
            var result = await backupService.CreateBackupAsync(null, options, cancellationToken);

            sw.Stop();

            if (result.Success)
            {
                _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, sw.Elapsed, true,
                    $"Backup completed: {result.FilePath} ({result.FileCount} files, {result.SizeBytes:N0} bytes)");

                _logger.LogInformation("Scheduled backup completed: {Path} ({Count} files, {Size:N0} bytes, {Duration}s)",
                    result.FilePath, result.FileCount, result.SizeBytes, sw.Elapsed.TotalSeconds);

                // Notification sending would go here (via IEventBus) when the
                // notification subscriber is extended to handle backup events.
                if (GetBoolSetting(settings, "Backup:NotifyOnSuccess"))
                {
                    _logger.LogInformation("Backup notification: success - {Path} ({Count} files, {Size})",
                        result.FilePath, result.FileCount, FormatSize(result.SizeBytes));
                }
            }
            else
            {
                _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, sw.Elapsed, false, result.ErrorMessage);
                _logger.LogError("Scheduled backup failed: {Error}", result.ErrorMessage);

                if (GetBoolSetting(settings, "Backup:NotifyOnFailure", defaultValue: true))
                {
                    _logger.LogWarning("Backup notification: failure - {Error}", result.ErrorMessage);
                }
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, sw.Elapsed, false, ex.Message);
            _logger.LogError(ex, "Scheduled backup threw an exception.");
        }
    }

    private static BackupOptions BuildBackupOptionsFromSettings(IReadOnlyList<SystemSettingDto> settings)
    {
        var options = new BackupOptions();
        foreach (var setting in settings)
        {
            switch (setting.Key)
            {
                case "Backup:IncludeDatabase":
                    options.IncludeDatabaseDump = bool.TryParse(setting.Value, out var includeDb) ? includeDb : true;
                    break;
                case "Backup:IncludeFileStorage":
                    options.IncludeFileStorage = bool.TryParse(setting.Value, out var includeFiles) ? includeFiles : true;
                    break;
                case "Backup:IncludeModuleData":
                    options.IncludeModuleData = bool.TryParse(setting.Value, out var includeModules) ? includeModules : true;
                    break;
                case "Backup:Directory":
                    options.BackupDirectory = setting.Value;
                    break;
            }
        }
        return options;
    }

    private static string GetStringSetting(IReadOnlyList<SystemSettingDto> settings, string key, string defaultValue = "")
    {
        return settings.FirstOrDefault(s => s.Key == key)?.Value ?? defaultValue;
    }

    private static bool GetBoolSetting(IReadOnlyList<SystemSettingDto> settings, string key, bool defaultValue = false)
    {
        var val = GetStringSetting(settings, key);
        return string.IsNullOrEmpty(val) ? defaultValue : bool.TryParse(val, out var b) ? b : defaultValue;
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
        _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB",
    };

}
