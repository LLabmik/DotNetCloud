namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Defines notification preference for a channel member.
/// </summary>
public enum NotificationPreference
{
    /// <summary>Receive notifications for all messages.</summary>
    All,

    /// <summary>Receive notifications only for @mentions.</summary>
    Mentions,

    /// <summary>Do not receive any notifications from this channel.</summary>
    None
}
