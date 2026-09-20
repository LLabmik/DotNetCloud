namespace DotNetCloud.Client.Android.Services;

/// <summary>One poll's outcome, in the shape the wake path needs to re-arm itself.</summary>
/// <param name="Outcome">What the poll produced.</param>
/// <param name="HasUnread">Whether unmuted unread messages remain (drives the aggressive cadence).</param>
public sealed record ChatAlertPollResult(ChatAlertPollOutcome Outcome, bool HasUnread)
{
    /// <summary>A result for a poll that found nothing to do at all.</summary>
    public static ChatAlertPollResult NoSession { get; } = new(ChatAlertPollOutcome.NoSession, false);
}

/// <summary>
/// Runs one background chat-alert poll: read the aggregate, decide, notify.
/// </summary>
/// <remarks>
/// <para>
/// This is the client half of the background alert transport
/// (<c>docs/ANDROID_CHAT_BACKGROUND_ALERTS_PLAN.md</c>): the phone asks the server for a tiny aggregate of
/// counts, and no companion app, push server, or Google service is involved at any point.
/// </para>
/// <para>
/// The poll is conditional — it echoes the previous entity-tag in <c>If-None-Match</c>, so an unchanged
/// instance answers <c>304</c> with no body and the server does not recompute the aggregate.
/// </para>
/// </remarks>
public interface IChatAlertPoller
{
    /// <summary>
    /// Performs a single poll.
    /// </summary>
    /// <param name="ct">
    /// Cancellation token. The wake path applies its own short budget — a Doze alarm receiver only gets a
    /// few seconds of execution, so a poll must never block indefinitely.
    /// </param>
    /// <returns>The outcome, including whether unmuted unread messages remain.</returns>
    Task<ChatAlertPollResult> PollAsync(CancellationToken ct = default);
}
