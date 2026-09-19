using Android.App;
using Android.Content;
using DotNetCloud.Client.Android.Services;
using static DotNetCloud.Client.Android.Services.AppBadgeManager;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Builds and posts the (always generic) chat and calendar notifications, from whichever path
/// learned about the event: a UnifiedPush message or an in-process SignalR message.
/// </summary>
/// <remarks>
/// Text comes exclusively from <see cref="UnifiedPushNotificationPlan"/>, which the client
/// derives from identifiers — never from message content. Notification ids are stable per target
/// so an update replaces the previous notification instead of stacking up.
/// </remarks>
internal static class UnifiedPushNotificationRenderer
{
    /// <summary>Intent extra carrying the chat channel to open on tap.</summary>
    public const string ExtraChannelId = "channelId";

    /// <summary>Intent extra carrying the calendar event to open on tap.</summary>
    public const string ExtraEventId = "eventId";

    /// <summary>Intent extra carrying the server connection the notification belongs to.</summary>
    public const string ExtraServerUrl = "serverUrl";

    private const string DmAcceptAction = "DOTNETCLOUD_DM_ACCEPT";
    private const string DmIgnoreAction = "DOTNETCLOUD_DM_IGNORE";
    private const string DmDndAction = "DOTNETCLOUD_DM_DND";

    /// <summary>Renders and posts a notification for a plan.</summary>
    /// <param name="context">Context used to post the notification.</param>
    /// <param name="plan">Plan to render; silent plans are ignored.</param>
    /// <param name="serverBaseUrl">Server connection the notification belongs to, when known.</param>
    /// <returns>True when a notification was posted.</returns>
    public static bool Render(Context? context, UnifiedPushNotificationPlan plan, string? serverBaseUrl)
    {
        if (context is null || plan.IsSilent)
            return false;

        try
        {
            var targetId = plan.TargetId;
            var seed = BuildSeed(plan, targetId);
            var contentIntent = PendingIntent.GetActivity(
                context,
                seed,
                BuildOpenIntent(context, plan, serverBaseUrl, targetId),
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
            var iconRes = ResolveIcon(context);

            var builder = new Notification.Builder(context, plan.NotificationChannelId)
                .SetContentTitle(plan.Title)
                .SetContentText(plan.Body)
                .SetSmallIcon(iconRes)
                .SetContentIntent(contentIntent)
                .SetAutoCancel(true);

            var notificationId = seed;

            switch (plan.Target)
            {
                case UnifiedPushNotificationTarget.CalendarEvent:
                    builder.SetCategory(Notification.CategoryAlarm);
                    break;

                case UnifiedPushNotificationTarget.Channel when targetId is not null:
                    builder.SetGroup($"dnc_chat_{targetId}");
                    break;
            }

            if (plan.Kind == UnifiedPushNotificationKind.DirectMessageInvite && targetId is not null)
            {
                AddDmActions(context, builder, targetId);
            }
            else if (plan.Kind != UnifiedPushNotificationKind.CalendarReminder)
            {
                builder.WithBadgeCount(context);
            }

            var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
            manager?.Notify(notificationId, builder.Build());

            if (plan.Kind == UnifiedPushNotificationKind.CalendarReminder)
                CancelLocalCalendarAlarm(context, targetId);

            return true;
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("DotNetCloud", $"Rendering a push notification failed: {ex.Message}");
            return false;
        }
    }

    private static Intent BuildOpenIntent(
        Context context,
        UnifiedPushNotificationPlan plan,
        string? serverBaseUrl,
        string? targetId)
    {
        var intent = new Intent(context, typeof(MainActivity));
        intent.SetAction(Intent.ActionMain);
        intent.AddCategory(Intent.CategoryLauncher);

        if (plan.Target == UnifiedPushNotificationTarget.Channel && targetId is not null)
            intent.PutExtra(ExtraChannelId, targetId);
        else if (plan.Target == UnifiedPushNotificationTarget.CalendarEvent && targetId is not null)
            intent.PutExtra(ExtraEventId, targetId);

        // Multi-connection routing: a tap must resolve against the server that sent the
        // notification, never whichever connection happens to be active.
        if (!string.IsNullOrWhiteSpace(serverBaseUrl))
            intent.PutExtra(ExtraServerUrl, serverBaseUrl);

        return intent;
    }

    private static void AddDmActions(Context context, Notification.Builder builder, string channelId)
    {
        builder.AddAction(BuildDmAction(context, DmAcceptAction, channelId, "Reply & Join", hash: 1));
        builder.AddAction(BuildDmAction(context, DmIgnoreAction, channelId, "Ignore", hash: 2));
        builder.AddAction(BuildDmAction(context, DmDndAction, channelId, "DND", hash: 3));
    }

    private static Notification.Action BuildDmAction(
        Context context, string action, string channelId, string title, int hash)
    {
        var intent = new Intent(context, typeof(DmNotificationActionReceiver));
        intent.SetAction(action);
        intent.PutExtra(ExtraChannelId, channelId);

        var pending = PendingIntent.GetBroadcast(
            context,
            channelId.GetHashCode() ^ hash,
            intent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        return new Notification.Action.Builder(null, title, pending).Build();
    }

    private static int BuildSeed(UnifiedPushNotificationPlan plan, string? targetId)
    {
        var value = targetId is null ? plan.Kind.GetHashCode() : StableHash(targetId);
        return plan.Kind switch
        {
            UnifiedPushNotificationKind.CalendarReminder => 5000 + (value & 0x0FFF),
            UnifiedPushNotificationKind.DirectMessageInvite => 6000 + (value & 0x0FFF),
            _ => 2000 + (value & 0x0FFF),
        };
    }

    /// <summary>
    /// Stable 31-bit hash (FNV-1a) used for notification and pending-intent ids.
    /// </summary>
    /// <remarks>
    /// <see cref="string.GetHashCode()"/> is randomized per process in .NET, so using it would give
    /// the same notification a different id after every app restart — a redelivered push would then
    /// stack a second notification instead of updating the first.
    /// </remarks>
    /// <param name="value">Value to hash.</param>
    /// <returns>A non-negative hash.</returns>
    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in value)
            {
                hash ^= character;
                hash *= 16777619u;
            }

            return (int)(hash & 0x7FFFFFFF);
        }
    }

    private static int ResolveIcon(Context context)
    {
        var icon = context.Resources?.GetIdentifier("ic_notification", "drawable", context.PackageName) ?? 0;
        return icon != 0 ? icon : global::Android.Resource.Drawable.IcDialogInfo;
    }

    private static void CancelLocalCalendarAlarm(Context context, string? eventId)
    {
        if (!Guid.TryParse(eventId, out var id))
            return;

        try
        {
            CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default
                .GetService<ICalendarReminderScheduler>()?.CancelReminders(id);
        }
        catch
        {
            // Best effort — a duplicate reminder is better than a crash.
        }
    }
}
