using Android.App;
using Android.Content;
using Android.Util;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Shared <see cref="AlarmManager"/> access for calendar reminders.
/// </summary>
/// <remarks>
/// The scheduling pass and the notification's snooze action arm their alarms through the same rules, so
/// a snooze is armed exactly like the alarm it replaces (and honours the same exact-alarm permission
/// state).
/// </remarks>
internal static class CalendarAlarmScheduling
{
    private const string LogTag = "DotNetCloud";

    /// <summary>Returns the system alarm service, or <c>null</c> when it is unavailable.</summary>
    /// <param name="context">Context to resolve the service from.</param>
    /// <returns>The alarm service, or <c>null</c>.</returns>
    internal static AlarmManager? GetAlarmManager(Context context)
        => context.GetSystemService(Context.AlarmService) as AlarmManager;

    /// <summary>
    /// Whether exact alarms may be armed. From API 31 they need the user-granted
    /// <c>SCHEDULE_EXACT_ALARM</c> permission; below that they never did.
    /// </summary>
    /// <param name="context">Context to resolve the alarm service from.</param>
    /// <returns><c>true</c> when an exact alarm can be scheduled.</returns>
    internal static bool CanScheduleExactAlarms(Context context)
    {
        if (global::Android.OS.Build.VERSION.SdkInt < global::Android.OS.BuildVersionCodes.S)
            return true; // API 30 and below: exact alarms don't need a separate permission

        var alarmManager = GetAlarmManager(context);
#pragma warning disable CA1416 // guarded by the SDK check above (API < S returns true early)
        return alarmManager?.CanScheduleExactAlarms() == true;
#pragma warning restore CA1416
    }

    /// <summary>Arms a one-shot RTC wakeup alarm, exact when the permission allows and inexact otherwise.</summary>
    /// <param name="context">Context to check the exact-alarm permission with.</param>
    /// <param name="alarmManager">The alarm service to arm through.</param>
    /// <param name="triggerMillis">Wall-clock trigger time, in milliseconds since the Unix epoch.</param>
    /// <param name="pendingIntent">The pending intent the alarm raises.</param>
    /// <returns><c>true</c> when the alarm was armed exactly.</returns>
    internal static bool Arm(
        Context context, AlarmManager alarmManager, long triggerMillis, PendingIntent pendingIntent)
    {
        if (CanScheduleExactAlarms(context))
        {
            alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerMillis, pendingIntent);
            return true;
        }

        Log.Warn(LogTag, "SCHEDULE_EXACT_ALARM not granted; armed an inexact calendar reminder alarm.");
        alarmManager.Set(AlarmType.RtcWakeup, triggerMillis, pendingIntent);
        return false;
    }
}
