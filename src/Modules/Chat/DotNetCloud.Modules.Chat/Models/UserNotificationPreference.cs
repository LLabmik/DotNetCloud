namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Persisted per-user push notification preferences (push enabled, do-not-disturb,
/// and muted channel IDs). Stored in the database so state is consistent across
/// devices, server restarts, and machines.
/// </summary>
public sealed class UserNotificationPreference
{
    /// <summary>Primary key — the owning user.</summary>
    public Guid UserId { get; set; }

    /// <summary>Whether push notifications are globally enabled. Defaults to true.</summary>
    public bool PushEnabled { get; set; } = true;

    /// <summary>Whether do-not-disturb mode is enabled. Defaults to false (disabled).</summary>
    public bool DoNotDisturb { get; set; }

    /// <summary>
    /// JSON-serialized set of muted channel IDs.
    /// Backs <c>UserNotificationPreferences.MutedChannelIds</c>.
    /// </summary>
    public string MutedChannelIdsJson { get; set; } = "[]";
}
