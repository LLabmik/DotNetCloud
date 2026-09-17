namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// How an incoming chat message should alert the user.
/// </summary>
public enum ChatAlertKind
{
    /// <summary>No alert at all (muted channel, own message echo, or dings switched off).</summary>
    None,

    /// <summary>
    /// Play the in-app ding. Used while the app is on screen, where no system notification is
    /// posted — without the ding the message would arrive completely silently.
    /// </summary>
    InAppSound,

    /// <summary>
    /// Post a system notification (the chat channel itself plays the notification sound).
    /// Used while the app is not visible.
    /// </summary>
    SystemNotification
}

/// <summary>
/// Decides how a new chat message alerts the user.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the web client's rule (<c>ChatPageLayout.ShouldPlayMessageSound</c>): a channel that is
/// muted never alerts, and a user's own message echo never alerts.
/// </para>
/// <para>
/// Exactly <b>one</b> alert is produced per message: while the app is visible the in-app ding is
/// used (system notifications are deliberately suppressed in that state), and while it is not
/// visible a system notification is posted, which brings its own sound.
/// </para>
/// </remarks>
public static class ChatAlertPolicy
{
    /// <summary>
    /// Decides which alert (if any) an incoming chat message should trigger.
    /// </summary>
    /// <param name="isForeground">Whether the app is currently visible to the user.</param>
    /// <param name="soundEnabled">Whether the user has the in-app chat sound enabled.</param>
    /// <param name="channelMuted">Whether the originating channel is muted.</param>
    /// <param name="senderUserId">User ID of the message sender (empty when unknown).</param>
    /// <param name="currentUserId">
    /// User ID of the signed-in user, or <see cref="Guid.Empty"/> when it could not be resolved.
    /// An unresolved ID only disables the own-message check — it never silences the alert.
    /// </param>
    /// <returns>The alert to perform.</returns>
    public static ChatAlertKind Decide(
        bool isForeground,
        bool soundEnabled,
        bool channelMuted,
        Guid senderUserId,
        Guid currentUserId)
    {
        if (channelMuted)
            return ChatAlertKind.None;

        // Not visible: a system notification is posted (and sounds) as it always has.
        if (!isForeground)
            return ChatAlertKind.SystemNotification;

        // Visible: notifications are suppressed, so the in-app ding is the only possible alert.
        if (!soundEnabled || senderUserId == Guid.Empty)
            return ChatAlertKind.None;

        // Own message echo (sent from this or another client) must never ding.
        return currentUserId != Guid.Empty && senderUserId == currentUserId
            ? ChatAlertKind.None
            : ChatAlertKind.InAppSound;
    }
}
