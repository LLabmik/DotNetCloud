using Android.App;
using Android.Content;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Manages the app icon badge count by reading the active notification count
/// from <see cref="NotificationManager"/> and applying it to new notifications
/// via <see cref="Notification.Builder.SetNumber(int)"/>.
/// </summary>
/// <remarks>
/// On Android 8.0+ (API 26), launchers that support numeric badges (Samsung One UI,
/// Pixel Launcher, etc.) read the count from <see cref="Notification.Builder.SetNumber(int)"/>.
/// All launchers show at least a badge dot when any notification is active.
/// </remarks>
public static class AppBadgeManager
{
    /// <summary>
    /// Gets the current notification badge count by counting active notifications
    /// in the chat-related notification channels.
    /// </summary>
    /// <param name="context">Android context.</param>
    /// <returns>The total number of active chat notifications.</returns>
    public static int GetActiveBadgeCount(Context context)
    {
        var nm = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (nm is null) return 0;

        var activeNotifications = nm.GetActiveNotifications();
        if (activeNotifications is null) return 0;

        var count = 0;
        foreach (var sbn in activeNotifications)
        {
            var channelId = sbn.Notification?.ChannelId;
            if (channelId is MainApplication.ChannelIdMessages
                          or MainApplication.ChannelIdMentions
                          or MainApplication.ChannelIdAnnouncements)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Applies the badge count to a <see cref="Notification.Builder"/> so that
    /// supported launchers display the numeric badge on the app icon.
    /// </summary>
    /// <param name="builder">The notification builder to configure.</param>
    /// <param name="context">Android context used to read active notification count.</param>
    /// <returns>The same builder for chaining.</returns>
    public static Notification.Builder WithBadgeCount(this Notification.Builder builder, Context context)
    {
        // +1 because the current notification being built hasn't been posted yet.
        var badgeCount = GetActiveBadgeCount(context) + 1;
        builder.SetNumber(badgeCount);
        return builder;
    }
}
