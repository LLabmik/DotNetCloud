using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Android implementation of <see cref="IChatAlertNotifier"/>.
/// </summary>
/// <remarks>
/// The decision carries a payload type and a channel id,
/// <see cref="NotificationPayloadContract.MapToNotification"/> turns those into generic text, and the
/// shared renderer posts the notification with the deep link. Both delivery paths therefore produce
/// identical, content-free notifications, and the "ignore any text the server sends" rule still holds.
/// </remarks>
internal sealed class ChatAlertNotifier : IChatAlertNotifier
{
    /// <inheritdoc />
    public bool Notify(ChatAlertDecision decision, string? serverBaseUrl)
    {
        var plan = NotificationPayloadContract.MapToNotification(new NotificationPayload
        {
            V = NotificationPayloadContract.PayloadVersion,
            Type = decision.PayloadType,
            ChannelId = decision.ChannelId?.ToString("D"),
        });

        return ChatNotificationRenderer.Render(
            global::Android.App.Application.Context, plan, serverBaseUrl);
    }
}
