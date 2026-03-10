using System.Collections.Concurrent;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// In-memory notification preference store for push routing and API endpoints.
/// </summary>
public sealed class InMemoryNotificationPreferenceStore : INotificationPreferenceStore
{
    private static readonly UserNotificationPreferences DefaultPreferences = new()
    {
        PushEnabled = true,
        DoNotDisturb = false,
        MutedChannelIds = new HashSet<Guid>()
    };

    private readonly ConcurrentDictionary<Guid, UserNotificationPreferences> _preferences = new();

    /// <inheritdoc />
    public UserNotificationPreferences Get(Guid userId)
    {
        return _preferences.GetOrAdd(userId, _ => DefaultPreferences);
    }

    /// <inheritdoc />
    public void Update(Guid userId, UserNotificationPreferences preferences)
    {
        var normalized = preferences with
        {
            MutedChannelIds = preferences.MutedChannelIds.Distinct().ToHashSet()
        };

        _preferences[userId] = normalized;
    }
}
