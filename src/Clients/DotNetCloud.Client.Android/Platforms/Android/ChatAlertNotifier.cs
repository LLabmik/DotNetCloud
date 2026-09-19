using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Android implementation of <see cref="IChatAlertNotifier"/>.
/// </summary>
/// <remarks>
/// Reuses the banked UnifiedPush rendering path verbatim: the decision carries a payload type and a
/// channel id, <see cref="UnifiedPushProtocol.MapToNotification"/> turns those into generic text, and the
/// shared renderer posts the notification with the deep link. Both transports therefore produce identical,
/// content-free notifications, and the "ignore any text the server sends" rule still holds.
/// </remarks>
internal sealed class ChatAlertNotifier : IChatAlertNotifier
{
    /// <inheritdoc />
    public bool Notify(ChatAlertDecision decision, string? serverBaseUrl)
    {
        var plan = UnifiedPushProtocol.MapToNotification(new UnifiedPushPayload
        {
            V = UnifiedPushProtocol.PayloadVersion,
            Type = decision.PayloadType,
            ChannelId = decision.ChannelId?.ToString("D"),
        });

        return UnifiedPushNotificationRenderer.Render(
            global::Android.App.Application.Context, plan, serverBaseUrl);
    }
}
