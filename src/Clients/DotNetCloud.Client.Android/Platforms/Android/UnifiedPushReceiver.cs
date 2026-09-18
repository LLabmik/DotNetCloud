using Android.App;
using Android.Content;
using CommunityToolkit.Mvvm.Messaging;
using DotNetCloud.Client.Android.Messages;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Receives everything a UnifiedPush distributor sends this app and drives the connector.
/// </summary>
/// <remarks>
/// <para>
/// This is a plain <see cref="BroadcastReceiver"/> implementing the end-user-application half of
/// the UnifiedPush Android specification (AND_3.1.0) directly — no UnifiedPush library exists for
/// .NET, so the protocol itself lives in <see cref="UnifiedPushProtocol"/>.
/// </para>
/// <para>
/// The process may be cold-started just for this broadcast, so <see cref="OnReceive"/> stays fast:
/// the notification and the acknowledgement broadcast are handled synchronously, while state
/// changes that need the network run fire-and-forget (they are idempotent and repeated on every
/// app start).
/// </para>
/// </remarks>
[BroadcastReceiver(Name = "net.dotnetcloud.client.UnifiedPushReceiver", Exported = true)]
[IntentFilter([UnifiedPushProtocol.ActionNewEndpoint,
               UnifiedPushProtocol.ActionRegistrationFailed,
               UnifiedPushProtocol.ActionMessage,
               UnifiedPushProtocol.ActionUnregistered,
               UnifiedPushProtocol.ActionTempUnavailable])]
public sealed class UnifiedPushReceiver : BroadcastReceiver
{
    private const string LogTag = "DotNetCloud";

    /// <inheritdoc />
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null)
            return;

        var token = intent.GetStringExtra(UnifiedPushProtocol.ExtraToken);

        try
        {
            switch (intent.Action)
            {
                case UnifiedPushProtocol.ActionNewEndpoint:
                    OnNewEndpoint(intent, token);
                    break;

                case UnifiedPushProtocol.ActionMessage:
                    OnPushMessage(context, intent, token);
                    break;

                case UnifiedPushProtocol.ActionRegistrationFailed:
                    RunConnector(c => c.HandleRegistrationFailedAsync(
                        token ?? string.Empty, intent.GetStringExtra(UnifiedPushProtocol.ExtraReason)));
                    break;

                case UnifiedPushProtocol.ActionUnregistered:
                    RunConnector(c => c.HandleUnregisteredAsync(
                        token ?? string.Empty, intent.GetStringExtra(UnifiedPushProtocol.ExtraUseDistributor)));
                    break;

                case UnifiedPushProtocol.ActionTempUnavailable:
                    RunConnector(c => c.HandleTempUnavailableAsync(
                        token ?? string.Empty, intent.GetStringExtra(UnifiedPushProtocol.ExtraUseDistributor)));
                    break;

                default:
                    global::Android.Util.Log.Debug(
                        LogTag, $"UnifiedPushReceiver: ignoring unhandled action '{intent.Action}'.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "UnifiedPush broadcast handling failed for action {Action}.", intent.Action);
        }
    }

    private static void OnNewEndpoint(Intent intent, string? token)
    {
        var endpoint = intent.GetStringExtra(UnifiedPushProtocol.ExtraEndpoint);
        var id = intent.GetStringExtra(UnifiedPushProtocol.ExtraId);

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(endpoint))
        {
            Logger.LogWarning("Ignoring a malformed NEW_ENDPOINT broadcast.");
            return;
        }

        global::Android.Util.Log.Info(
            LogTag, $"UnifiedPush NEW_ENDPOINT for host {UnifiedPushProtocol.SafeEndpointHost(endpoint)}.");

        RunConnector(c => c.HandleNewEndpointAsync(token, endpoint, id));
    }

    private static void OnPushMessage(Context context, Intent intent, string? token)
    {
        var id = intent.GetStringExtra(UnifiedPushProtocol.ExtraId);

        // The specification requires broadcasts for an unknown connection token to be ignored —
        // that includes not acknowledging them, so a stale or misrouted push cannot be mistaken
        // for a delivered one. A fresh install with no registration therefore stays quiet.
        var registration = string.IsNullOrWhiteSpace(token)
            ? null
            : TryResolve<IUnifiedPushRegistrationStore>()?.FindByToken(token);

        if (registration is null)
        {
            Logger.LogWarning("Ignoring a UnifiedPush message for an unknown connection token.");
            return;
        }

        var payload = UnifiedPushProtocol.ParsePayload(
            intent.GetByteArrayExtra(UnifiedPushProtocol.ExtraBytesMessage));

        if (payload is null)
        {
            // Acknowledge anyway: the distributor delivered it, and a redelivery would be identical.
            Logger.LogWarning("Discarding an unreadable UnifiedPush message.");
            Acknowledge(token, id);
            return;
        }

        var plan = UnifiedPushProtocol.MapToNotification(payload);
        var serverBaseUrl = registration.ServerBaseUrl;

        global::Android.Util.Log.Info(
            LogTag, $"UnifiedPush message: kind={plan.Kind}, target={plan.TargetId}, server={serverBaseUrl}.");

        if (plan.IsSilent)
        {
            // A calendar change: refresh the calendar instead of notifying.
            WeakReferenceMessenger.Default.Send<CalendarEventChangedMessage>(new());
            Acknowledge(token, id);
            return;
        }

        if (ShouldSuppress(payload))
        {
            Acknowledge(token, id);
            return;
        }

        UnifiedPushNotificationRenderer.Render(context, plan, serverBaseUrl);
        Acknowledge(token, id);
    }

    /// <summary>Whether an in-app path already covers this message.</summary>
    private static bool ShouldSuppress(UnifiedPushPayload payload)
    {
        try
        {
            if (TryResolve<IAppForegroundService>()?.IsInForeground == true)
            {
                Logger.LogDebug("App is in the foreground; its own alert covers this message.");
                return true;
            }

            if (Guid.TryParse(payload.ChannelId, out var channelId)
                && TryResolve<IChannelMuteStateService>()?.IsMuted(channelId) == true)
            {
                Logger.LogDebug("Channel is muted; suppressing the notification.");
                return true;
            }
        }
        catch (Exception ex)
        {
            // Best effort — notifying is safer than dropping a message.
            Logger.LogDebug(ex, "Suppression checks failed.");
        }

        return false;
    }

    private static void Acknowledge(string? token, string? id)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        RunConnector(connector => connector.AcknowledgeAsync(token, id));
    }

    private static void RunConnector(Func<IUnifiedPushConnector, Task> work)
    {
        var connector = TryResolve<IUnifiedPushConnector>();
        if (connector is null)
        {
            Logger.LogWarning("The UnifiedPush connector is unavailable; the broadcast was ignored.");
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await work(connector).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "UnifiedPush connector work failed.");
            }
        });
    }

    private static T? TryResolve<T>() where T : class
    {
        try
        {
            return CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetService<T>();
        }
        catch
        {
            // The container is not ready yet (or was disposed); treat it as absent.
            return null;
        }
    }

    private static ILogger Logger =>
        TryResolve<ILogger<UnifiedPushReceiver>>() ?? NullLogger<UnifiedPushReceiver>.Instance;
}
