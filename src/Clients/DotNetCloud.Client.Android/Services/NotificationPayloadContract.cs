using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// The DotNetCloud notification payload contract (version 1) and the mapping from a payload to the
/// generic notification text this app renders.
/// </summary>
/// <remarks>
/// <para>
/// <b>Payload privacy rule.</b> The server sends identifiers only — never a title, body, sender
/// name or channel name. The client ignores any such fields it receives and always renders the
/// generic text produced by <see cref="MapToNotification"/> (defence in depth: a server
/// regression must never leak text into a notification).
/// </para>
/// <para>
/// Both live delivery paths — the in-app SignalR alert and the background alert poll — go through
/// <see cref="MapToNotification"/>, so they produce identical, content-free notifications.
/// </para>
/// <para>
/// Deliberately free of Android dependencies so the contract is unit-testable on plain
/// <c>net10.0</c>.
/// </para>
/// </remarks>
public static class NotificationPayloadContract
{
    // ── Payload contract (version 1) ─────────────────────────────────────────

    /// <summary>Version of the ID-only payload contract this client understands.</summary>
    public const int PayloadVersion = 1;

    /// <summary>An ordinary chat message arrived.</summary>
    public const string PayloadTypeMessage = "message";

    /// <summary>The user was mentioned in a channel.</summary>
    public const string PayloadTypeMention = "mention";

    /// <summary>An announcement was posted.</summary>
    public const string PayloadTypeAnnouncement = "announcement";

    /// <summary>Someone started a direct message channel with the user.</summary>
    public const string PayloadTypeDmChannelCreated = "dm_channel_created";

    /// <summary>A calendar reminder fired for the user.</summary>
    public const string PayloadTypeCalendarReminder = "calendar_reminder";

    /// <summary>
    /// Calendar data changed. Sent by the core server outside the versioned contract and used
    /// purely as a refresh signal: it must never raise a notification.
    /// </summary>
    public const string PayloadTypeCalendarEvent = "calendar_event";

    // ── Notification channel ids (single source of truth) ────────────────────
    // MainApplication re-exports these so there is exactly one definition.

    /// <summary>Channel id for ordinary chat messages.</summary>
    public const string ChannelMessages = "chat_messages";

    /// <summary>Channel id for @mention alerts.</summary>
    public const string ChannelMentions = "chat_mentions";

    /// <summary>Channel id for announcements.</summary>
    public const string ChannelAnnouncements = "chat_announcements";

    /// <summary>Channel id for direct-message invitations.</summary>
    public const string ChannelDmNotifications = "dm_notifications";

    /// <summary>Channel id for calendar reminders.</summary>
    public const string ChannelCalendarReminders = "calendar_reminders";

    /// <summary>
    /// Maps a payload to the generic notification this app renders.
    /// </summary>
    /// <remarks>
    /// Never reads a title, body or name from the payload: every string returned here is chosen
    /// by the client. Unknown types fall back to a generic notification so a future server type
    /// still reaches the user; <see cref="PayloadTypeCalendarEvent"/> maps to a silent plan.
    /// </remarks>
    /// <param name="payload">Parsed payload; null is treated as an unreadable message.</param>
    /// <returns>The notification plan.</returns>
    public static NotificationPlan MapToNotification(NotificationPayload? payload)
    {
        var channelId = NormalizeGuid(payload?.ChannelId);
        var eventId = NormalizeGuid(payload?.EventId) ?? channelId;
        var type = payload?.Type?.Trim().ToLowerInvariant() ?? string.Empty;

        return type switch
        {
            PayloadTypeMessage => Chat(
                NotificationKind.Message, "New message", ChannelMessages, channelId),

            PayloadTypeMention => Chat(
                NotificationKind.Mention, "You were mentioned", ChannelMentions, channelId),

            PayloadTypeAnnouncement => Chat(
                NotificationKind.Announcement, "New announcement", ChannelAnnouncements, channelId),

            PayloadTypeDmChannelCreated => Chat(
                NotificationKind.DirectMessageInvite,
                "New direct message",
                ChannelDmNotifications,
                channelId),

            PayloadTypeCalendarReminder => new NotificationPlan
            {
                Kind = NotificationKind.CalendarReminder,
                Title = "Calendar reminder",
                Body = string.Empty,
                NotificationChannelId = ChannelCalendarReminders,
                Target = eventId is null
                    ? NotificationTarget.None
                    : NotificationTarget.CalendarEvent,
                TargetId = eventId,
            },

            PayloadTypeCalendarEvent => new NotificationPlan
            {
                Kind = NotificationKind.Silent,
                Title = string.Empty,
                Body = string.Empty,
                NotificationChannelId = ChannelMessages,
                Target = NotificationTarget.None,
                TargetId = null,
            },

            _ => Chat(NotificationKind.Generic, "New notification", ChannelMessages, channelId),
        };
    }

    private static NotificationPlan Chat(
        NotificationKind kind,
        string title,
        string channel,
        string? channelId) => new()
        {
            Kind = kind,
            Title = title,
            Body = string.Empty,
            NotificationChannelId = channel,
            Target = channelId is null
                ? NotificationTarget.None
                : NotificationTarget.Channel,
            TargetId = channelId,
        };

    private static string? NormalizeGuid(string? value) =>
        Guid.TryParse(value, out var id) ? id.ToString("D") : null;
}

/// <summary>
/// The version-1 DotNetCloud notification payload: identifiers only, never user-visible text.
/// </summary>
/// <remarks>
/// There are deliberately no title/body/sender properties — a payload containing them is parsed
/// successfully and those fields are discarded, which is what keeps notifications generic even
/// if a server-side builder regresses.
/// </remarks>
public sealed record NotificationPayload
{
    /// <summary>Contract version; diagnostics only.</summary>
    [JsonPropertyName("v")]
    public int? V { get; init; }

    /// <summary>Notification type, e.g. <see cref="NotificationPayloadContract.PayloadTypeMessage"/>.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>Chat channel the notification belongs to.</summary>
    [JsonPropertyName("channelId")]
    public string? ChannelId { get; init; }

    /// <summary>Message the notification belongs to, when applicable.</summary>
    [JsonPropertyName("messageId")]
    public string? MessageId { get; init; }

    /// <summary>Calendar event the notification belongs to, when applicable.</summary>
    [JsonPropertyName("eventId")]
    public string? EventId { get; init; }
}

/// <summary>What a payload asks the client to do.</summary>
public enum NotificationKind
{
    /// <summary>Refresh-only signal — no notification (e.g. a calendar change).</summary>
    Silent = 0,

    /// <summary>An ordinary chat message.</summary>
    Message,

    /// <summary>The user was mentioned.</summary>
    Mention,

    /// <summary>An announcement was posted.</summary>
    Announcement,

    /// <summary>A direct-message channel was created (carries Accept/Ignore/DND actions).</summary>
    DirectMessageInvite,

    /// <summary>A calendar reminder.</summary>
    CalendarReminder,

    /// <summary>An unrecognised type, rendered generically.</summary>
    Generic,
}

/// <summary>Where a notification tap should take the user.</summary>
public enum NotificationTarget
{
    /// <summary>No deep link; just open the app.</summary>
    None = 0,

    /// <summary>Open the chat channel in <see cref="NotificationPlan.TargetId"/>.</summary>
    Channel,

    /// <summary>Open the calendar event in <see cref="NotificationPlan.TargetId"/>.</summary>
    CalendarEvent,
}

/// <summary>
/// The generic notification a payload maps to: text is chosen by the client, never by the server.
/// </summary>
public sealed record NotificationPlan
{
    /// <summary>Kind of notification.</summary>
    public required NotificationKind Kind { get; init; }

    /// <summary>Generic notification title.</summary>
    public required string Title { get; init; }

    /// <summary>Notification body; intentionally empty under the generic-notification rule.</summary>
    public required string Body { get; init; }

    /// <summary>Android notification channel id to post on.</summary>
    public required string NotificationChannelId { get; init; }

    /// <summary>Deep-link target for a tap.</summary>
    public required NotificationTarget Target { get; init; }

    /// <summary>Identifier for <see cref="Target"/>, when applicable.</summary>
    public string? TargetId { get; init; }

    /// <summary>Whether this payload must not raise a notification at all.</summary>
    public bool IsSilent => Kind == NotificationKind.Silent;
}
