using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Android UnifiedPush specification (AND_3.1.0) constants, the DotNetCloud push payload
/// contract (version 1) and the mapping from a payload to generic notification text.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately free of Android dependencies so the whole protocol is unit-testable on plain
/// <c>net10.0</c>. Anything that needs an <c>Android.Content.Intent</c> lives in
/// <c>Platforms/Android/UnifiedPushIntents.cs</c>.
/// </para>
/// <para>
/// <b>Payload privacy rule.</b> The server sends identifiers only — never a title, body, sender
/// name or channel name. The client ignores any such fields it receives and always renders the
/// generic text produced by <see cref="MapToNotification"/> (defence in depth: a server
/// regression must never leak text into a notification).
/// </para>
/// <para>See <c>docs/ANDROID_UNIFIEDPUSH_PLAN.md</c> §4.2 and §4.4.</para>
/// </remarks>
public static class UnifiedPushProtocol
{
    // ── Connector → distributor broadcasts ───────────────────────────────────

    /// <summary>Action used to ask a distributor to register this app (spec §Registration).</summary>
    public const string ActionRegister = "org.unifiedpush.android.distributor.REGISTER";

    /// <summary>Action used to ask a distributor to unregister this app.</summary>
    public const string ActionUnregister = "org.unifiedpush.android.distributor.UNREGISTER";

    /// <summary>Action used to acknowledge a received message or endpoint ping.</summary>
    public const string ActionMessageAck = "org.unifiedpush.android.distributor.MESSAGE_ACK";

    // ── Distributor → app broadcasts ─────────────────────────────────────────

    /// <summary>Broadcast carrying a new (or refreshed) push endpoint.</summary>
    public const string ActionNewEndpoint = "org.unifiedpush.android.connector.NEW_ENDPOINT";

    /// <summary>Broadcast reporting that a registration could not be created.</summary>
    public const string ActionRegistrationFailed = "org.unifiedpush.android.connector.REGISTRATION_FAILED";

    /// <summary>Broadcast carrying a push message from the push server.</summary>
    public const string ActionMessage = "org.unifiedpush.android.connector.MESSAGE";

    /// <summary>Broadcast reporting that a registration no longer exists.</summary>
    public const string ActionUnregistered = "org.unifiedpush.android.connector.UNREGISTERED";

    /// <summary>Broadcast reporting that the distributor's push server is unavailable.</summary>
    public const string ActionTempUnavailable = "org.unifiedpush.android.connector.TEMP_UNAVAILABLE";

    /// <summary>
    /// Action of the service a distributor may bind to temporarily raise this app to foreground
    /// importance. This is <i>not</i> a foreground service and consumes no service budget.
    /// </summary>
    public const string ActionRaiseToForeground = "org.unifiedpush.android.connector.RAISE_TO_FOREGROUND";

    // ── Intent extras ────────────────────────────────────────────────────────

    /// <summary>Connection token identifying one registration.</summary>
    public const string ExtraToken = "token";

    /// <summary>Push endpoint URL supplied by the distributor.</summary>
    public const string ExtraEndpoint = "endpoint";

    /// <summary>Message/id ping identifier that must be acknowledged.</summary>
    public const string ExtraId = "id";

    /// <summary>Raw push message bytes.</summary>
    public const string ExtraBytesMessage = "bytesMessage";

    /// <summary>Failure reason supplied with <see cref="ActionRegistrationFailed"/>.</summary>
    public const string ExtraReason = "reason";

    /// <summary>Package name of a distributor the connector should switch to.</summary>
    public const string ExtraUseDistributor = "useDistributor";

    /// <summary>Pending intent used to identify the calling app (targetSdk &lt; 34 only).</summary>
    public const string ExtraPendingIntent = "pi";

    /// <summary>Optional short description shown in the distributor's UI.</summary>
    public const string ExtraRegistrationMessage = "message";

    // ── Registration failure reasons ─────────────────────────────────────────

    /// <summary>Generic distributor error; the connector may retry immediately.</summary>
    public const string ReasonInternalError = "INTERNAL_ERROR";

    /// <summary>The distributor has no network; retry when connectivity returns.</summary>
    public const string ReasonNetwork = "NETWORK";

    /// <summary>The distributor needs a user action before it can register us.</summary>
    public const string ReasonActionRequired = "ACTION_REQUIRED";

    /// <summary>The distributor requires a VAPID key, which this app does not provide.</summary>
    public const string ReasonVapidRequired = "VAPID_REQUIRED";

    // ── Spec limits ──────────────────────────────────────────────────────────

    /// <summary>Maximum size, in bytes, of a push message (spec §Resources).</summary>
    public const int MaxMessageBytes = 4096;

    /// <summary>Maximum size, in bytes, of a connection token or message id.</summary>
    public const int MaxTokenBytes = 100;

    /// <summary>Maximum size, in bytes, of the registration description.</summary>
    public const int MaxRegistrationMessageBytes = 100;

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

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Creates a fresh connection token (a lowercase UUID, 36 bytes — well within the
    /// specification's 100-byte limit).
    /// </summary>
    /// <returns>A new connection token.</returns>
    public static string CreateToken() => Guid.NewGuid().ToString("D");

    /// <summary>
    /// Builds the <see cref="ActionRegister"/> broadcast descriptor.
    /// </summary>
    /// <param name="token">Connection token for this registration.</param>
    /// <param name="registrationDescription">
    /// Optional short description the distributor may display (truncated to the spec limit).
    /// </param>
    /// <returns>The broadcast descriptor.</returns>
    /// <exception cref="ArgumentException">The token is missing or too long.</exception>
    public static UnifiedPushIntentDescriptor BuildRegisterIntent(
        string token, string? registrationDescription = null)
    {
        var extras = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ExtraToken] = RequireToken(token),
        };

        var description = Truncate(registrationDescription, MaxRegistrationMessageBytes);
        if (!string.IsNullOrEmpty(description))
            extras[ExtraRegistrationMessage] = description;

        // targetSdk >= 34 identifies the sender via FLAG_SHARE_IDENTITY instead of a
        // PendingIntent — see UnifiedPushIntents.ToAndroidIntent.
        return new UnifiedPushIntentDescriptor(ActionRegister, extras, ShareIdentity: true);
    }

    /// <summary>Builds the <see cref="ActionUnregister"/> broadcast descriptor.</summary>
    /// <param name="token">Connection token of the registration to drop.</param>
    /// <returns>The broadcast descriptor.</returns>
    /// <exception cref="ArgumentException">The token is missing or too long.</exception>
    public static UnifiedPushIntentDescriptor BuildUnregisterIntent(string token) =>
        new(ActionUnregister,
            new Dictionary<string, string>(StringComparer.Ordinal) { [ExtraToken] = RequireToken(token) },
            ShareIdentity: true);

    /// <summary>Builds the <see cref="ActionMessageAck"/> broadcast descriptor.</summary>
    /// <param name="token">Connection token the acknowledgement belongs to.</param>
    /// <param name="id">Identifier supplied by the distributor; null or empty produces no ack.</param>
    /// <returns>The broadcast descriptor, or null when there is nothing to acknowledge.</returns>
    /// <exception cref="ArgumentException">The token is missing or too long.</exception>
    public static UnifiedPushIntentDescriptor? BuildAckIntent(string token, string? id)
    {
        var messageId = Truncate(id, MaxTokenBytes);
        if (string.IsNullOrEmpty(messageId))
            return null;

        return new UnifiedPushIntentDescriptor(
            ActionMessageAck,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ExtraToken] = RequireToken(token),
                [ExtraId] = messageId,
            },
            ShareIdentity: false);
    }

    /// <summary>
    /// Parses a push message into the ID-only payload contract.
    /// </summary>
    /// <param name="message">Raw bytes delivered by the distributor.</param>
    /// <returns>The parsed payload, or null when it is empty, oversized or not valid JSON.</returns>
    public static UnifiedPushPayload? ParsePayload(byte[]? message)
    {
        if (message is null || message.Length is 0 or > MaxMessageBytes)
            return null;

        try
        {
            return JsonSerializer.Deserialize<UnifiedPushPayload>(message, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

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
    public static UnifiedPushNotificationPlan MapToNotification(UnifiedPushPayload? payload)
    {
        var channelId = NormalizeGuid(payload?.ChannelId);
        var eventId = NormalizeGuid(payload?.EventId) ?? channelId;
        var type = payload?.Type?.Trim().ToLowerInvariant() ?? string.Empty;

        return type switch
        {
            PayloadTypeMessage => Chat(
                UnifiedPushNotificationKind.Message, "New message", ChannelMessages, channelId),

            PayloadTypeMention => Chat(
                UnifiedPushNotificationKind.Mention, "You were mentioned", ChannelMentions, channelId),

            PayloadTypeAnnouncement => Chat(
                UnifiedPushNotificationKind.Announcement, "New announcement", ChannelAnnouncements, channelId),

            PayloadTypeDmChannelCreated => Chat(
                UnifiedPushNotificationKind.DirectMessageInvite,
                "New direct message",
                ChannelDmNotifications,
                channelId),

            PayloadTypeCalendarReminder => new UnifiedPushNotificationPlan
            {
                Kind = UnifiedPushNotificationKind.CalendarReminder,
                Title = "Calendar reminder",
                Body = string.Empty,
                NotificationChannelId = ChannelCalendarReminders,
                Target = eventId is null
                    ? UnifiedPushNotificationTarget.None
                    : UnifiedPushNotificationTarget.CalendarEvent,
                TargetId = eventId,
            },

            PayloadTypeCalendarEvent => new UnifiedPushNotificationPlan
            {
                Kind = UnifiedPushNotificationKind.Silent,
                Title = string.Empty,
                Body = string.Empty,
                NotificationChannelId = ChannelMessages,
                Target = UnifiedPushNotificationTarget.None,
                TargetId = null,
            },

            _ => Chat(UnifiedPushNotificationKind.Generic, "New notification", ChannelMessages, channelId),
        };
    }

    /// <summary>
    /// Extracts only the host (and port) of an endpoint for logging or display, so the
    /// capability URL — whose topic is a secret — never reaches a log or the UI.
    /// </summary>
    /// <param name="endpoint">Endpoint URL.</param>
    /// <returns>The host, or null when the endpoint is missing or unparsable.</returns>
    public static string? SafeEndpointHost(string? endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ? uri.Authority : null;

    /// <summary>Truncates a value to a maximum number of UTF-8 bytes; null or blank gives null.</summary>
    /// <param name="value">Value to truncate.</param>
    /// <param name="maxBytes">Maximum length in bytes.</param>
    /// <returns>The truncated value, or null.</returns>
    public static string? Truncate(string? value, int maxBytes)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (System.Text.Encoding.UTF8.GetByteCount(trimmed) <= maxBytes)
            return trimmed;

        var builder = new System.Text.StringBuilder();
        var used = 0;
        foreach (var rune in trimmed.EnumerateRunes())
        {
            var size = System.Text.Encoding.UTF8.GetByteCount(rune.ToString());
            if (used + size > maxBytes)
                break;

            builder.Append(rune);
            used += size;
        }

        return builder.ToString();
    }

    private static UnifiedPushNotificationPlan Chat(
        UnifiedPushNotificationKind kind,
        string title,
        string channel,
        string? channelId) => new()
        {
            Kind = kind,
            Title = title,
            Body = string.Empty,
            NotificationChannelId = channel,
            Target = channelId is null
                ? UnifiedPushNotificationTarget.None
                : UnifiedPushNotificationTarget.Channel,
            TargetId = channelId,
        };

    private static string RequireToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || System.Text.Encoding.UTF8.GetByteCount(token) > MaxTokenBytes)
            throw new ArgumentException("A connection token is required and must be at most 100 bytes.", nameof(token));

        return token;
    }

    private static string? NormalizeGuid(string? value) =>
        Guid.TryParse(value, out var id) ? id.ToString("D") : null;
}

/// <summary>
/// A distributor → app or connector → distributor broadcast, expressed without Android types.
/// </summary>
/// <param name="Action">The broadcast action.</param>
/// <param name="Extras">String extras to attach.</param>
/// <param name="ShareIdentity">
/// Whether the broadcast must be sent with the SDK 34+ shared-identity flag (the REGISTER and
/// UNREGISTER contract requires it, because this app targets SDK 35).
/// </param>
public sealed record UnifiedPushIntentDescriptor(
    string Action,
    IReadOnlyDictionary<string, string> Extras,
    bool ShareIdentity);

/// <summary>
/// The version-1 DotNetCloud push payload: identifiers only, never user-visible text.
/// </summary>
/// <remarks>
/// There are deliberately no title/body/sender properties — a payload containing them is parsed
/// successfully and those fields are discarded, which is what keeps notifications generic even
/// if a server-side builder regresses.
/// </remarks>
public sealed record UnifiedPushPayload
{
    /// <summary>Contract version; diagnostics only.</summary>
    [JsonPropertyName("v")]
    public int? V { get; init; }

    /// <summary>Notification type, e.g. <see cref="UnifiedPushProtocol.PayloadTypeMessage"/>.</summary>
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
public enum UnifiedPushNotificationKind
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
public enum UnifiedPushNotificationTarget
{
    /// <summary>No deep link; just open the app.</summary>
    None = 0,

    /// <summary>Open the chat channel in <see cref="UnifiedPushNotificationPlan.TargetId"/>.</summary>
    Channel,

    /// <summary>Open the calendar event in <see cref="UnifiedPushNotificationPlan.TargetId"/>.</summary>
    CalendarEvent,
}

/// <summary>
/// The generic notification a payload maps to: text is chosen by the client, never by the server.
/// </summary>
public sealed record UnifiedPushNotificationPlan
{
    /// <summary>Kind of notification.</summary>
    public required UnifiedPushNotificationKind Kind { get; init; }

    /// <summary>Generic notification title.</summary>
    public required string Title { get; init; }

    /// <summary>Notification body; intentionally empty under the generic-notification rule.</summary>
    public required string Body { get; init; }

    /// <summary>Android notification channel id to post on.</summary>
    public required string NotificationChannelId { get; init; }

    /// <summary>Deep-link target for a tap.</summary>
    public required UnifiedPushNotificationTarget Target { get; init; }

    /// <summary>Identifier for <see cref="Target"/>, when applicable.</summary>
    public string? TargetId { get; init; }

    /// <summary>Whether this payload must not raise a notification at all.</summary>
    public bool IsSilent => Kind == UnifiedPushNotificationKind.Silent;
}
