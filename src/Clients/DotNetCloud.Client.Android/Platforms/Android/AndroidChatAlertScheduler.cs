using Android.App;
using Android.App.Job;
using Android.Content;
using Android.OS;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Android wake path for the background chat-alert poll: a persisted one-shot
/// <see cref="JobScheduler"/> job, plus an allow-while-idle alarm while the device dozes.
/// </summary>
/// <remarks>
/// <para>
/// <b>No foreground service, by design.</b> The chat <c>dataSync</c> FGS was removed permanently because
/// Android 15/16 caps it to a rolling 24-hour budget and crash-loops the app once exhausted
/// (<c>ForegroundServiceDidNotStopInTimeException</c>). Everything here is deliberately outside that
/// budget: a job is accounted separately, and an allow-while-idle alarm is not a service at all.
/// </para>
/// <para>
/// <b>Why two mechanisms.</b> Doze defers ordinary jobs to a maintenance window, so a job alone cannot
/// bound the latency while the device is idle; an allow-while-idle alarm does fire in Doze, but only when
/// the user has granted the exact-alarm special access, which the calendar reminders already ask for.
/// When that access is missing the job stays the only mechanism and the interval simply stretches.
/// </para>
/// <para>
/// <see cref="Schedule"/> is idempotent, so it is safe on every process start — which also re-arms the
/// chain if a run was killed before it could reschedule itself. The persisted job survives a reboot on
/// its own (that is what <c>SetPersisted(true)</c> buys, and why no boot receiver is needed).
/// </para>
/// </remarks>
internal sealed class AndroidChatAlertScheduler : IChatAlertScheduler
{
    /// <summary>
    /// Slack added on top of the minimum latency before the override deadline fires, so a dispatched job
    /// still honours the requested delay in the normal case. Without a deadline a min-latency-only
    /// one-shot was measured being deferred <i>indefinitely</i> on this device.
    /// </summary>
    private const long DeadlineSlackMillis = 60 * 1000; // 1 minute

    /// <summary>Request code for the Doze alarm's pending intent.</summary>
    private const int AlarmRequestCode = 3108;

    private readonly IExactAlarmPermissionService _exactAlarms;
    private readonly IServerConnectionStore _serverConnections;
    private readonly ILogger<AndroidChatAlertScheduler>? _logger;

    /// <summary>Initializes a new <see cref="AndroidChatAlertScheduler"/>.</summary>
    /// <param name="exactAlarms">Exact-alarm permission probe.</param>
    /// <param name="serverConnections">Store used to detect that there is nothing to poll.</param>
    /// <param name="logger">Logger.</param>
    public AndroidChatAlertScheduler(
        IExactAlarmPermissionService exactAlarms,
        IServerConnectionStore serverConnections,
        ILogger<AndroidChatAlertScheduler>? logger = null)
    {
        _exactAlarms = exactAlarms;
        _serverConnections = serverConnections;
        _logger = logger;
    }

    /// <inheritdoc />
    public void Schedule()
    {
        try
        {
            if (Platform.AppContext is not { } context)
                return;

            if (_serverConnections.GetActive() is null)
            {
                Cancel();
                return;
            }

            if (IsQueued(context))
                return; // already queued; calling this on every process start is idempotent

            ScheduleJob(context, ChatAlertCadence.Idle);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to schedule the background chat alert poll.");
        }
    }

    /// <inheritdoc />
    public void ScheduleAfterRun(ChatAlertPollResult result)
    {
        try
        {
            if (Platform.AppContext is not { } context)
                return;

            // Clear both mechanisms first: the newly resolved cadence must always win.
            CancelQueued(context);

            if (result.Outcome == ChatAlertPollOutcome.NoSession)
                return; // nothing to poll; the chain restarts on the next process start

            var state = new ChatAlertPollState(
                HasSession: true,
                HasUnread: result.HasUnread,
                IsDozing: IsDozing(context));

            if (ChatAlertCadence.ResolveNextDelay(state) is not { } delay)
                return;

            if (ChatAlertCadence.RequiresExactAlarm(state, _exactAlarms.HasExactAlarmPermission()))
                ScheduleAlarm(context, delay);
            else
                ScheduleJob(context, delay);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to re-arm the background chat alert poll.");
        }
    }

    /// <inheritdoc />
    public void Cancel()
    {
        try
        {
            if (Platform.AppContext is { } context)
                CancelQueued(context);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to cancel the background chat alert poll.");
        }
    }

    /// <summary>True when either the job or the Doze alarm is already queued.</summary>
    private static bool IsQueued(Context context) => IsJobQueued(context) || IsAlarmQueued(context);

    /// <summary>True when the poll job is already pending.</summary>
    private static bool IsJobQueued(Context context)
    {
        if (context.GetSystemService(Context.JobSchedulerService) is not JobScheduler scheduler)
            return false;

        var pending = scheduler.AllPendingJobs;
        if (pending is null)
            return false;

        foreach (var job in pending)
        {
            if (job.Id == ChatAlertJobService.JobId)
                return true;
        }

        return false;
    }

    /// <summary>True when the Doze alarm is already pending.</summary>
    /// <remarks>
    /// <c>NoCreate</c> makes this a pure lookup, so probing for the alarm never creates one.
    /// </remarks>
    private static bool IsAlarmQueued(Context context)
    {
        var intent = new Intent(context, typeof(ChatAlertAlarmReceiver));
        intent.SetAction(ChatAlertAlarmReceiver.AlarmAction);

        return PendingIntent.GetBroadcast(
            context,
            AlarmRequestCode,
            intent,
            PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable) is not null;
    }

    /// <summary>Removes both the job and the alarm.</summary>
    private void CancelQueued(Context context)
    {
        if (context.GetSystemService(Context.JobSchedulerService) is JobScheduler scheduler)
            scheduler.Cancel(ChatAlertJobService.JobId);

        if (context.GetSystemService(Context.AlarmService) is AlarmManager alarms
            && BuildAlarmPendingIntent(context) is { } pending)
        {
            alarms.Cancel(pending);
        }
    }

    /// <summary>Queues a single one-shot poll after the supplied delay.</summary>
    private void ScheduleJob(Context context, TimeSpan delay)
    {
        if (context.GetSystemService(Context.JobSchedulerService) is not JobScheduler scheduler)
            return;

        if (Java.Lang.Class.FromType(typeof(ChatAlertJobService)) is not { } serviceClass)
            return;

        var delayMillis = (long)delay.TotalMilliseconds;
        var component = new ComponentName(context, serviceClass);
        var builder = new JobInfo.Builder(ChatAlertJobService.JobId, component);

        // Any network, NOT Unmetered: the media sync job is Wi-Fi-only, but a chat alert must work on
        // cellular or it is not an alert at all.
        builder.SetRequiredNetworkType(NetworkType.Any);
        builder.SetPersisted(true);
        builder.SetMinimumLatency(delayMillis);

        // Without an override deadline a min-latency-only one-shot can be deferred indefinitely.
        builder.SetOverrideDeadline(delayMillis + DeadlineSlackMillis);

        if (builder.Build() is { } info)
        {
            var result = scheduler.Schedule(info);
            _logger?.LogInformation(
                "Chat alert poll scheduled in {Seconds}s (result={Result}).",
                (int)delay.TotalSeconds, result);
        }
    }

    /// <summary>Queues the Doze-piercing alarm that runs the poll directly.</summary>
    private void ScheduleAlarm(Context context, TimeSpan delay)
    {
        if (context.GetSystemService(Context.AlarmService) is not AlarmManager alarms
            || BuildAlarmPendingIntent(context) is not { } pending)
        {
            return;
        }

        // ElapsedRealtime wakes the device from sleep and is immune to clock changes.
        var triggerAtMillis = SystemClock.ElapsedRealtime() + (long)delay.TotalMilliseconds;

        if (_exactAlarms.HasExactAlarmPermission())
        {
            alarms.SetExactAndAllowWhileIdle(AlarmType.ElapsedRealtimeWakeup, triggerAtMillis, pending);
            _logger?.LogInformation(
                "Chat alert poll armed for Doze in {Minutes}m (exact).", (int)delay.TotalMinutes);
        }
        else
        {
            // No exact-alarm access: still ask for an allow-while-idle delivery, just without a guarantee.
            alarms.SetAndAllowWhileIdle(AlarmType.ElapsedRealtimeWakeup, triggerAtMillis, pending);
            _logger?.LogInformation(
                "Chat alert poll armed for Doze in {Minutes}m (inexact — exact alarms not granted).",
                (int)delay.TotalMinutes);
        }
    }

    /// <summary>Builds (or updates) the pending intent used by the Doze alarm.</summary>
    private static PendingIntent? BuildAlarmPendingIntent(Context context)
    {
        var intent = new Intent(context, typeof(ChatAlertAlarmReceiver));
        intent.SetAction(ChatAlertAlarmReceiver.AlarmAction);
        return PendingIntent.GetBroadcast(
            context,
            AlarmRequestCode,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
    }

    /// <summary>True while the platform considers the device idle (Doze).</summary>
    private static bool IsDozing(Context context) =>
        context.GetSystemService(Context.PowerService) is PowerManager power
        && power.IsDeviceIdleMode;
}
