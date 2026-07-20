using Android.App;
using Android.Content;
using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android;

/// <summary>
/// BroadcastReceiver that fires after device boot and reschedules all calendar
/// reminder alarms via <see cref="ICalendarReminderScheduler.RescheduleAllAsync"/>.
/// </summary>
/// <remarks>
/// Declared in AndroidManifest.xml with <c>android.permission.RECEIVE_BOOT_COMPLETED</c>
/// and an intent-filter for <c>android.intent.action.BOOT_COMPLETED</c>.
/// </remarks>
[BroadcastReceiver(Exported = true)]
[IntentFilter(["android.intent.action.BOOT_COMPLETED"])]
public sealed class CalendarBootReceiver : BroadcastReceiver
{
    /// <inheritdoc />
    public override async void OnReceive(Context? context, Intent? intent)
    {
        var logger = SafeResolveLogger();
        logger?.LogInformation("CalendarBootReceiver: device boot detected, rescheduling alarms.");

        try
        {
            var scheduler = Ioc.Default.GetService<ICalendarReminderScheduler>();
            if (scheduler is null)
            {
                logger?.LogWarning("CalendarBootReceiver: ICalendarReminderScheduler not available.");
                return;
            }

            await scheduler.RescheduleAllAsync().ConfigureAwait(false);
            logger?.LogInformation("CalendarBootReceiver: alarms rescheduled successfully.");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "CalendarBootReceiver: failed to reschedule alarms.");
        }
    }

    private static ILogger<CalendarBootReceiver>? SafeResolveLogger()
    {
        try { return Ioc.Default.GetService<ILogger<CalendarBootReceiver>>(); }
        catch { return null; }
    }
}
