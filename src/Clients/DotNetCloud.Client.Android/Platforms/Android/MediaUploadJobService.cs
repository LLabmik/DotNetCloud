using Android.App;
using Android.App.Job;
using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Runs a bounded media auto-upload scan when the operating system wakes the app in the
/// background, so camera-roll backup continues while the app is closed.
/// </summary>
/// <remarks>
/// <para>
/// The media watcher itself is deliberately in-process (see <c>MediaAutoUploadService</c>): it uses
/// <b>no</b> foreground service, so it consumes none of the Android 15/16 <c>dataSync</c>
/// 24-hour budget that makes <c>ChatConnectionService</c> crash-loop once exhausted. The trade-off
/// is that nothing runs while the process is dead — this <see cref="JobService"/> supplies the
/// missing trigger. <see cref="JobScheduler"/> is used (rather than a foreground service or
/// <c>AlarmManager</c>) because it is Doze-aware, honours network constraints, survives reboots,
/// and is accounted separately from the foreground-service budget.
/// </para>
/// <para>
/// The job is <b>one-shot and re-arms itself</b> after each run (see
/// <c>IBackgroundMediaSync.ScheduleAfterRun</c>): every few minutes while media is still queued,
/// otherwise hourly. Returning <c>true</c> from <see cref="OnStartJob"/> keeps the process alive
/// (with a wake lock) until <see cref="JobService.JobFinished"/> is called, which is what gives the
/// scan time to record progress.
/// </para>
/// </remarks>
[Service(
    Name = "net.dotnetcloud.client.MediaUploadJobService",
    Permission = "android.permission.BIND_JOB_SERVICE",
    Exported = false)]
public sealed class MediaUploadJobService : JobService
{
    /// <summary>Stable job id used to schedule (and detect) the periodic background sync job.</summary>
    internal const int JobId = 3107;

    /// <summary>
    /// Upper bound on a single headless scan. Capped well below the platform's ~10-minute job
    /// limit so <see cref="JobService.JobFinished"/> is always reached cleanly.
    /// </summary>
    private static readonly TimeSpan MaxRunDuration = TimeSpan.FromMinutes(8);

    /// <inheritdoc />
    public override bool OnStartJob(JobParameters? parameters)
    {
        if (parameters is null)
            return false;

        var logger = Ioc.Default.GetService<ILogger<MediaUploadJobService>>();
        logger?.LogInformation("Background media sync job started (headless).");

        // OnStartJob runs on the main thread — move the scan off it. Returning true keeps the
        // process alive until JobFinished; the platform holds a wake lock for us meanwhile.
        _ = Task.Run(async () =>
        {
            var hasPendingWork = false;
            try
            {
                var upload = Ioc.Default.GetService<IMediaAutoUploadService>();
                if (upload is null)
                {
                    logger?.LogWarning("Background media sync: IMediaAutoUploadService is unavailable.");
                    return;
                }

                using var cts = new CancellationTokenSource(MaxRunDuration);
                await upload.ScanAndUploadNowAsync(cts.Token).ConfigureAwait(false);
                hasPendingWork = upload.HasPendingWork;
                logger?.LogInformation(
                    "Background media sync job finished one scan pass (pending work: {Pending}).",
                    hasPendingWork);
            }
            catch (OperationCanceledException)
            {
                // Treat a timed-out pass as unfinished so the next run is not parked on the idle
                // cadence while media is still queued.
                hasPendingWork = true;
                logger?.LogInformation("Background media sync job hit its time budget; will resume shortly.");
            }
            catch (Exception ex)
            {
                hasPendingWork = true;
                logger?.LogWarning(ex, "Background media sync job failed; will retry shortly.");
            }
            finally
            {
                try
                {
                    JobFinished(parameters, wantsReschedule: false);
                }
                catch (Exception ex)
                {
                    logger?.LogDebug(ex, "JobFinished threw; ignoring.");
                }

                // This is a one-shot job, so the chain must be re-armed explicitly: poll every few
                // minutes while media is still backed up, otherwise fall back to the idle cadence.
                // Done after JobFinished so the finished job no longer holds the job id.
                Ioc.Default.GetService<IBackgroundMediaSync>()?.ScheduleAfterRun(hasPendingWork);
            }
        });

        return true;
    }

    /// <inheritdoc />
    public override bool OnStopJob(JobParameters? parameters)
    {
        // The system stopped us (constraints lost, or a newer run arrived). Ask to be
        // rescheduled so the periodic schedule continues.
        return true;
    }
}
