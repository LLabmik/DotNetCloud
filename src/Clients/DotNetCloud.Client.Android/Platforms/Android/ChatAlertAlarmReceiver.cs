using Android.App;
using Android.Content;
using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Alarm fired while the device is dozing, which runs one chat-alert poll directly.
/// </summary>
/// <remarks>
/// <para>
/// Doze defers ordinary <c>JobScheduler</c> work to a maintenance window, so the only way to bound the
/// latency of a poll while the phone sits idle is an allow-while-idle alarm. That alarm briefly allowlists
/// the app, but a broadcast receiver still only gets a few seconds — hence <see cref="BroadcastReceiver.GoAsync"/>
/// plus a hard timeout, and a body that does the smallest possible amount of work: one conditional GET,
/// which the server usually answers with an empty <c>304</c>.
/// </para>
/// <para>
/// The poll re-arms the chain itself, so this receiver never schedules anything directly — the shared
/// cadence policy decides whether the next wake is another alarm or an ordinary job.
/// </para>
/// <para>
/// ⚠️ The explicit <c>Name</c> matters: a receiver declared both here and in <c>AndroidManifest.xml</c>
/// without one lands in a generated <c>crc…</c> Java package, and the manifest entry then points at a
/// class that does not exist. For the same reason this attribute must stay <b>attribute-identical</b> to
/// its manifest entry (note the absent <c>Enabled</c>: setting it emits
/// <c>android:enabled="true"</c> and the manifest merger then rejects the pair as a genuine duplicate).
/// </para>
/// </remarks>
[BroadcastReceiver(
    Name = "net.dotnetcloud.client.ChatAlertAlarmReceiver",
    Exported = false)]
public sealed class ChatAlertAlarmReceiver : BroadcastReceiver
{
    /// <summary>Action identifying this alarm's pending intent.</summary>
    internal const string AlarmAction = "net.dotnetcloud.client.action.CHAT_ALERT_ALARM";

    /// <summary>
    /// Budget for the alarm's poll. Deliberately short: the platform only grants a few seconds of
    /// execution here, and anything unfinished is retried on the next cadence tick.
    /// </summary>
    private static readonly TimeSpan MaxRunDuration = TimeSpan.FromSeconds(8);

    /// <inheritdoc />
    public override void OnReceive(Context? context, Intent? intent)
    {
        var logger = Ioc.Default.GetService<ILogger<ChatAlertAlarmReceiver>>();
        var pendingResult = GoAsync();

        _ = Task.Run(async () =>
        {
            var result = new ChatAlertPollResult(ChatAlertPollOutcome.Failed, HasUnread: true);

            try
            {
                var poller = Ioc.Default.GetService<IChatAlertPoller>();
                if (poller is not null)
                {
                    using var cts = new CancellationTokenSource(MaxRunDuration);
                    result = await poller.PollAsync(cts.Token).ConfigureAwait(false);
                    logger?.LogInformation(
                        "Chat alert Doze poll finished ({Outcome}, pending: {Pending}).",
                        result.Outcome, result.HasUnread);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Chat alert Doze poll failed; will retry shortly.");
            }
            finally
            {
                try
                {
                    pendingResult?.Finish();
                }
                catch (Exception ex)
                {
                    logger?.LogDebug(ex, "Finishing the alarm's pending result threw; ignoring.");
                }

                Ioc.Default.GetService<IChatAlertScheduler>()?.ScheduleAfterRun(result);
            }
        });
    }
}
