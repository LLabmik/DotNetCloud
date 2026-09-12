using Android.App.Job;
using Android.Content;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Android implementation of <see cref="IBackgroundMediaSync"/> backed by
/// <see cref="JobScheduler"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses a persisted <b>one-shot</b> job that re-arms itself after every run. A periodic job cannot
/// provide the active cadence because <c>JobInfo.Builder.SetPeriodic</c> is clamped to a 15-minute
/// platform minimum, whereas the point of the active interval is to poll every 5 minutes while a
/// backfill is draining. <c>SetMinimumLatency</c> has no such floor.
/// </para>
/// <para>
/// The cadence adapts: when the last scan still had media queued the next run is
/// <see cref="ActiveDelayMillis"/> away, otherwise it falls back to <see cref="IdleDelayMillis"/>.
/// The job requires an unmetered (Wi-Fi) network, matching the watcher's own Wi-Fi-only gate, and
/// deliberately avoids any foreground service — so it consumes none of the Android 15/16
/// <c>dataSync</c> 24-hour budget that makes <c>ChatConnectionService</c> crash-loop once exhausted.
/// </para>
/// <para>
/// <see cref="Schedule"/> is idempotent: it inspects the pending jobs first, so it is safe to call
/// on every process start — which also re-arms the chain if an earlier run was killed before it
/// could reschedule itself.
/// </para>
/// </remarks>
internal sealed class AndroidBackgroundMediaSync : IBackgroundMediaSync
{
    /// <summary>Poll interval used while media is still waiting to be uploaded.</summary>
    private const long ActiveDelayMillis = 5 * 60 * 1000; // 5 minutes

    /// <summary>Fallback interval used once the upload queue is empty.</summary>
    private const long IdleDelayMillis = 60 * 60 * 1000; // 1 hour

    /// <summary>
    /// Slack added on top of the minimum latency before the override deadline fires, so a
    /// dispatched job still honours the requested delay in the normal case.
    /// </summary>
    private const long DeadlineSlackMillis = 2 * 60 * 1000; // 2 minutes

    private readonly ILogger<AndroidBackgroundMediaSync>? _logger;

    /// <summary>Initializes a new <see cref="AndroidBackgroundMediaSync"/>.</summary>
    public AndroidBackgroundMediaSync(ILogger<AndroidBackgroundMediaSync>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void Schedule()
    {
        try
        {
            if (Platform.AppContext is not { } context)
                return;

            if (context.GetSystemService(Context.JobSchedulerService) is not JobScheduler scheduler)
                return;

            if (IsAlreadyScheduled(scheduler))
                return; // already queued; calling this on every process start is idempotent

            ScheduleNext(context, scheduler, IdleDelayMillis);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to schedule the background media sync job.");
        }
    }

    /// <inheritdoc />
    public void ScheduleAfterRun(bool pendingWork)
    {
        try
        {
            if (Platform.AppContext is not { } context)
                return;

            if (context.GetSystemService(Context.JobSchedulerService) is not JobScheduler scheduler)
                return;

            // The run that just finished is complete, but clear any stale queued copy so the new
            // delay always wins — Schedule otherwise fails with RESULT_FAILURE on a duplicate id.
            scheduler.Cancel(MediaUploadJobService.JobId);

            ScheduleNext(context, scheduler, pendingWork ? ActiveDelayMillis : IdleDelayMillis);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to schedule the next background media sync run.");
        }
    }

    /// <summary>True when a background media sync job is already queued.</summary>
    private static bool IsAlreadyScheduled(JobScheduler scheduler)
    {
        var pending = scheduler.AllPendingJobs;
        if (pending is null)
            return false;

        foreach (var job in pending)
        {
            if (job.Id == MediaUploadJobService.JobId)
                return true;
        }

        return false;
    }

    /// <summary>Queues a single one-shot run of the media sync job after the supplied delay.</summary>
    private void ScheduleNext(Context context, JobScheduler scheduler, long delayMillis)
    {
        // ComponentName takes a Java.Lang.Class, and the JobInfo.Builder fluent setters return a
        // nullable Builder in the binding — use statements, not a chain.
        if (Java.Lang.Class.FromType(typeof(MediaUploadJobService)) is not { } serviceClass)
            return;

        var component = new ComponentName(context, serviceClass);
        var builder = new JobInfo.Builder(MediaUploadJobService.JobId, component);
        builder.SetRequiredNetworkType(NetworkType.Unmetered);
        builder.SetPersisted(true);
        builder.SetMinimumLatency(delayMillis);

        // A one-shot job with only a minimum latency may be deferred indefinitely by the platform
        // (observed on this Samsung device: TIME went negative and the job stayed RUNNABLE without
        // ever being dispatched). An override deadline bounds that: the system must run the job
        // once the deadline passes. Use a small slack so the normal case still respects the delay.
        builder.SetOverrideDeadline(delayMillis + DeadlineSlackMillis);

        if (builder.Build() is not { } info)
            return;

        var result = scheduler.Schedule(info);
        _logger?.LogInformation(
            "Background media sync scheduled in {Minutes}m (result={Result}).",
            delayMillis / 60000, result);
    }

    /// <inheritdoc />
    public void Cancel()
    {
        try
        {
            if (Platform.AppContext is not { } context)
                return;

            if (context.GetSystemService(Context.JobSchedulerService) is not JobScheduler scheduler)
                return;

            scheduler.Cancel(MediaUploadJobService.JobId);
            _logger?.LogInformation("Background media sync job cancelled.");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to cancel the background media sync job.");
        }
    }
}
