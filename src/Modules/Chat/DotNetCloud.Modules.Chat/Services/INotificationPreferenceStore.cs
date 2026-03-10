namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Stores and retrieves caller-level notification preferences used by push routing.
/// </summary>
public interface INotificationPreferenceStore
{
    /// <summary>
    /// Gets preferences for the specified user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns>The current preferences, or defaults when none were stored.</returns>
    UserNotificationPreferences Get(Guid userId);

    /// <summary>
    /// Updates preferences for the specified user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="preferences">The new preference values.</param>
    void Update(Guid userId, UserNotificationPreferences preferences);
}

/// <summary>
/// Immutable preference model for push-delivery controls.
/// </summary>
public sealed record UserNotificationPreferences
{
    /// <summary>
    /// Gets a value indicating whether push notifications are globally enabled.
    /// </summary>
    public bool PushEnabled { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether do-not-disturb mode is enabled.
    /// </summary>
    public bool DoNotDisturb { get; init; }

    /// <summary>
    /// Gets channel IDs muted for push notifications.
    /// </summary>
    public IReadOnlySet<Guid> MutedChannelIds { get; init; } = new HashSet<Guid>();
}
