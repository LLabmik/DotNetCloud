using Android.App;
using Android.App.Job;
using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Runs one background chat-alert poll when the operating system wakes the app, so the phone still learns
/// about new messages while the app is closed.
/// </summary>
/// <remarks>
/// <para>
/// This is the wake path of the adopted "our code only" transport
/// (<c>docs/ANDROID_UNIFIEDPUSH_PLAN.md</c> §12): a <b>poll</b> of the server's tiny aggregate endpoint,
/// using <b>no</b> foreground service, no push server, and nothing from Google. The trade-off accepted
/// with the operator is best-effort latency (seconds while the phone is in use, minutes when it is idle).
/// </para>
/// <para>
/// The job is <b>one-shot and re-arms itself</b> (see <see cref="IChatAlertScheduler.ScheduleAfterRun"/>):
/// a minute while unmuted messages are waiting, otherwise five. Returning <c>true</c> from
/// <see cref="OnStartJob"/> keeps the process alive, with a wake lock, until
/// <see cref="JobService.JobFinished"/> is called.
/// </para>
/// </remarks>
[Service(
    Name = "net.dotnetcloud.client.ChatAlertJobService",
    Permission = "android.permission.BIND_JOB_SERVICE",
    Exported = false)]
public sealed class ChatAlertJobService : JobService
{
    /// <summary>Stable job id used to schedule (and detect) the background chat-alert poll.</summary>
    internal const int JobId = 3108;

    /// <summary>
    /// Upper bound on a single poll. Far below the platform's ~10-minute job limit, but generous for one
    /// small GET — a poll that hangs must never hold the job open.
    /// </summary>
    private static readonly TimeSpan MaxRunDuration = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public override bool OnStartJob(JobParameters? parameters)
    {
        if (parameters is null)
            return false;

        var logger = Ioc.Default.GetService<ILogger<ChatAlertJobService>>();
        logger?.LogInformation("Background chat alert poll started (headless).");

        // OnStartJob runs on the main thread — move the poll off it. Returning true keeps the process
        // alive until JobFinished; the platform holds a wake lock for us meanwhile.
        _ = Task.Run(async () =>
        {
            // Failures default to "retry soon" rather than "stop the chain".
            var result = new ChatAlertPollResult(ChatAlertPollOutcome.Failed, HasUnread: true);

            try
            {
                var poller = Ioc.Default.GetService<IChatAlertPoller>();
                if (poller is null)
                {
                    logger?.LogWarning("Background chat alert poll: IChatAlertPoller is unavailable.");
                    return;
                }

                using var cts = new CancellationTokenSource(MaxRunDuration);
                result = await poller.PollAsync(cts.Token).ConfigureAwait(false);
                logger?.LogInformation(
                    "Background chat alert poll finished ({Outcome}, pending: {Pending}).",
                    result.Outcome, result.HasUnread);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Background chat alert poll failed; will retry shortly.");
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

                // This is a one-shot job, so the chain must be re-armed explicitly. Done after
                // JobFinished so the finished job no longer holds the job id.
                Ioc.Default.GetService<IChatAlertScheduler>()?.ScheduleAfterRun(result);
            }
        });

        return true;
    }

    /// <inheritdoc />
    public override bool OnStopJob(JobParameters? parameters)
    {
        // The system stopped us (constraints lost, or a newer run arrived). Ask to be rescheduled so the
        // chain continues instead of silently dying.
        return true;
    }
}
