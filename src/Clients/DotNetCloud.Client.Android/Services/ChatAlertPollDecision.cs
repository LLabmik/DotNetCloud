namespace DotNetCloud.Client.Android.Services;

/// <summary>What one background chat-alert poll produced.</summary>
public enum ChatAlertPollOutcome
{
    /// <summary>No saved server connection — there is nothing to poll, and the chain stops.</summary>
    NoSession,

    /// <summary>The server reported the aggregate unchanged (HTTP 304).</summary>
    UpToDate,

    /// <summary>The aggregate was read and a notification was posted.</summary>
    Alerted,

    /// <summary>The aggregate was read but deliberately not notified (already seen, or suppressed).</summary>
    Suppressed,

    /// <summary>The poll failed (network, auth, malformed response); the next run retries.</summary>
    Failed
}

/// <summary>
/// The outcome of applying <see cref="ChatAlertPollDecision.Decide"/> to one aggregate.
/// </summary>
public sealed record ChatAlertDecision
{
    /// <summary>True when a system notification should be posted.</summary>
    public bool ShouldAlert { get; init; }

    /// <summary>
    /// Payload type to render — one of the <see cref="UnifiedPushProtocol"/> payload constants, reused so
    /// the poll transport produces exactly the same generic text as the push transport would.
    /// </summary>
    public string PayloadType { get; init; } = UnifiedPushProtocol.PayloadTypeMessage;

    /// <summary>Channel to open when the notification is tapped, when known.</summary>
    public Guid? ChannelId { get; init; }

    /// <summary>
    /// The change this decision accounts for. The caller persists it as its high-water mark — whether it
    /// alerted or deliberately stayed silent — so the same message can never alert twice.
    /// </summary>
    public DateTime? AcknowledgedChangedAtUtc { get; init; }
}

/// <summary>
/// Decides whether a freshly read aggregate warrants a notification, and with which wording.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately pure and Android-free so it is unit-testable on plain <c>net10.0</c>, mirroring the
/// banked split that keeps the notification payload contract free of Android types
/// (<c>UnifiedPushProtocol</c>).
/// </para>
/// <para>
/// <b>No double alerts.</b> The poll transport and the SignalR/in-app path must not both fire for one
/// message, so this decision is keyed on the server's monotonic change value (the newest message time)
/// and the app's persisted high-water mark: a message can only raise an alert when the change value is
/// strictly newer than the last one the app accounted for. Reading a channel does not move that value,
/// so clearing messages can never re-alert them.
/// </para>
/// <para>
/// <b>Mute is absolute.</b> Only the <c>unmuted*</c> counts can trigger an alert, so a mention inside a
/// muted channel is counted by the server but never reaches the user — matching
/// <see cref="ChatAlertPolicy"/>, which also refuses to alert for a muted channel.
/// </para>
/// </remarks>
public static class ChatAlertPollDecision
{
    /// <summary>
    /// Decides what to do about an aggregate.
    /// </summary>
    /// <param name="summary">The aggregate just read, or null when the response was unusable.</param>
    /// <param name="lastAcknowledgedChangedAtUtc">
    /// The newest change the app has already accounted for (alerted, or seen while the app was on
    /// screen), or null when it has never accounted for one.
    /// </param>
    /// <returns>The decision to apply.</returns>
    public static ChatAlertDecision Decide(
        ChatAlertsSummary? summary,
        DateTime? lastAcknowledgedChangedAtUtc)
    {
        if (summary is null)
            return new ChatAlertDecision();

        // A muted channel can never alert, so its unread messages are irrelevant here.
        if (summary.UnmutedUnread <= 0)
            return new ChatAlertDecision();

        if (EnsureUtc(summary.ChangedAt) is not { } changedAt)
            return new ChatAlertDecision();

        if (lastAcknowledgedChangedAtUtc is { } last && changedAt <= EnsureUtc(last))
            return new ChatAlertDecision();

        return new ChatAlertDecision
        {
            ShouldAlert = true,
            PayloadType = summary.UnmutedMentions > 0
                ? UnifiedPushProtocol.PayloadTypeMention
                : UnifiedPushProtocol.PayloadTypeMessage,
            ChannelId = summary.TopChannelId,
            AcknowledgedChangedAtUtc = changedAt,
        };
    }

    /// <summary>
    /// Normalizes a timestamp to UTC. JSON deserialization drops <see cref="DateTimeKind"/>, which turns
    /// an unspecified value into local time when it is later compared or formatted — the same trap that
    /// made calendar alarms fire hours off.
    /// </summary>
    /// <param name="value">Timestamp to normalize.</param>
    /// <returns>The UTC timestamp, or null.</returns>
    internal static DateTime? EnsureUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } utc => utc,
        { Kind: DateTimeKind.Local } local => local.ToUniversalTime(),
        { } unspecified => DateTime.SpecifyKind(unspecified, DateTimeKind.Utc),
    };
}
