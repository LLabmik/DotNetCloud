using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Database-backed implementation of <see cref="INotificationPreferenceStore"/>.
/// Persists push/DND/mute preferences in the chat schema so state is consistent
/// across devices and survives server restarts.
/// </summary>
public sealed class DbNotificationPreferenceStore : INotificationPreferenceStore
{
    private readonly IDbContextFactory<ChatDbContext> _contextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DbNotificationPreferenceStore"/> class.
    /// </summary>
    /// <param name="contextFactory">Factory used to create short-lived <see cref="ChatDbContext"/> instances.</param>
    public DbNotificationPreferenceStore(IDbContextFactory<ChatDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public UserNotificationPreferences Get(Guid userId)
    {
        using var db = _contextFactory.CreateDbContext();
        var entity = db.UserNotificationPreferences.Find(userId);
        if (entity is null)
        {
            // New user — defaults: push enabled, DND disabled, no muted channels.
            return new UserNotificationPreferences();
        }

        return new UserNotificationPreferences
        {
            PushEnabled = entity.PushEnabled,
            DoNotDisturb = entity.DoNotDisturb,
            MutedChannelIds = DeserializeMutedChannels(entity.MutedChannelIdsJson)
        };
    }

    /// <inheritdoc />
    public void Update(Guid userId, UserNotificationPreferences preferences)
    {
        var normalized = preferences with
        {
            MutedChannelIds = preferences.MutedChannelIds.Distinct().ToHashSet()
        };

        using var db = _contextFactory.CreateDbContext();
        var entity = db.UserNotificationPreferences.Find(userId);
        if (entity is null)
        {
            db.UserNotificationPreferences.Add(new UserNotificationPreference
            {
                UserId = userId,
                PushEnabled = normalized.PushEnabled,
                DoNotDisturb = normalized.DoNotDisturb,
                MutedChannelIdsJson = SerializeMutedChannels(normalized.MutedChannelIds)
            });
        }
        else
        {
            entity.PushEnabled = normalized.PushEnabled;
            entity.DoNotDisturb = normalized.DoNotDisturb;
            entity.MutedChannelIdsJson = SerializeMutedChannels(normalized.MutedChannelIds);
        }

        db.SaveChanges();
    }

    private static string SerializeMutedChannels(IReadOnlySet<Guid> ids) => JsonSerializer.Serialize(ids);

    private static IReadOnlySet<Guid> DeserializeMutedChannels(string json)
    {
        try
        {
            return (JsonSerializer.Deserialize<Guid[]>(json) ?? []).ToHashSet();
        }
        catch (JsonException)
        {
            return new HashSet<Guid>();
        }
    }
}
