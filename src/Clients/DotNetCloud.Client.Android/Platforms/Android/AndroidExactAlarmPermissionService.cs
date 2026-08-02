using Android.App;
using Android.Content;
using Android.OS;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;
using Application = Android.App.Application;

namespace DotNetCloud.Client.Android.Platforms.Android;

/// <summary>
/// Android implementation of <see cref="IExactAlarmPermissionService"/>.
/// Checks <see cref="AlarmManager.CanScheduleExactAlarms"/> and opens
/// the system's exact alarm permission settings page for the app.
/// </summary>
internal sealed class AndroidExactAlarmPermissionService : IExactAlarmPermissionService
{
    private readonly ILogger<AndroidExactAlarmPermissionService> _logger;

    /// <summary>Initializes a new <see cref="AndroidExactAlarmPermissionService"/>.</summary>
    public AndroidExactAlarmPermissionService(ILogger<AndroidExactAlarmPermissionService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool HasExactAlarmPermission()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.S)
            return true; // Pre-Android 12: no permission needed

        var context = Application.Context;
        var alarmManager = context.GetSystemService(Context.AlarmService) as AlarmManager;
#pragma warning disable CA1416 // guarded by the SDK check above (API < S returns true early)
        return alarmManager?.CanScheduleExactAlarms() == true;
#pragma warning restore CA1416
    }

    /// <inheritdoc />
    public void OpenPermissionSettings()
    {
        if (HasExactAlarmPermission())
        {
            _logger.LogDebug("Exact alarm permission already granted; skipping prompt.");
            return;
        }

        try
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.S)
                return; // SCHEDULE_EXACT_ALARM settings intent only exists on Android 12+

            var context = Application.Context;
#pragma warning disable CA1416 // guarded by the SDK check above
            var intent = new Intent(
                global::Android.Provider.Settings.ActionRequestScheduleExactAlarm,
                global::Android.Net.Uri.FromParts("package", context.PackageName, null))
                .AddFlags(ActivityFlags.NewTask);
#pragma warning restore CA1416

            context.StartActivity(intent);
            _logger.LogInformation("Opened SCHEDULE_EXACT_ALARM permission settings.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open exact alarm permission settings.");
        }
    }
}
