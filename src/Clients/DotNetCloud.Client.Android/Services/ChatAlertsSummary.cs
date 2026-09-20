using System.Text.Json.Serialization;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// The aggregate alert summary returned by <c>GET /api/v1/chat/alerts</c>, inside the standard
/// <c>{ success, data }</c> envelope.
/// </summary>
/// <remarks>
/// <para>
/// Carries <b>no message content, no sender name and no channel name</b> by design — the notification
/// text is always chosen on the device (see <see cref="NotificationPayloadContract.MapToNotification"/>),
/// so nothing readable ever leaves the server. The client reads only the counts and the tap target.
/// </para>
/// <para>
/// This is the poll transport's equivalent of the ID-only payload contract
/// (<c>docs/ANDROID_CHAT_BACKGROUND_ALERTS_PLAN.md</c>): the phone asks, the server answers with numbers.
/// </para>
/// </remarks>
public sealed record ChatAlertsSummary
{
    /// <summary>Payload contract version reported by the server.</summary>
    [JsonPropertyName("v")]
    public int V { get; init; } = 1;

    /// <summary>Total unread messages across every channel, including muted ones.</summary>
    [JsonPropertyName("unread")]
    public int Unread { get; init; }

    /// <summary>Unread messages that mention the user, including mentions in muted channels.</summary>
    [JsonPropertyName("mentions")]
    public int Mentions { get; init; }

    /// <summary>
    /// Unread messages in channels the user has <b>not</b> muted — the only count that may raise an
    /// alert, because a muted channel must never produce one.
    /// </summary>
    [JsonPropertyName("unmutedUnread")]
    public int UnmutedUnread { get; init; }

    /// <summary>Unread mentions in channels the user has <b>not</b> muted — selects the alert wording.</summary>
    [JsonPropertyName("unmutedMentions")]
    public int UnmutedMentions { get; init; }

    /// <summary>Unmuted channel holding the most recent unread message, used as the tap target.</summary>
    [JsonPropertyName("topChannelId")]
    public Guid? TopChannelId { get; init; }

    /// <summary>Sent time (UTC) of the most recent message in any of the user's channels.</summary>
    [JsonPropertyName("changedAt")]
    public DateTime? ChangedAt { get; init; }
}
