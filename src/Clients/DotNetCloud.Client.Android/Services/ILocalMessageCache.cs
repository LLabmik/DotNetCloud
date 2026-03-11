namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Caches recent chat messages locally so the app can display content while offline
/// or while a fresh server fetch is in progress.
/// </summary>
public interface ILocalMessageCache
{
    /// <summary>Returns the most recent <paramref name="count"/> messages for a channel.</summary>
    Task<IReadOnlyList<CachedMessage>> GetRecentAsync(Guid channelId, int count = 50, CancellationToken ct = default);

    /// <summary>Upserts messages into the local cache.</summary>
    Task UpsertAsync(IEnumerable<CachedMessage> messages, CancellationToken ct = default);

    /// <summary>Removes all cached messages older than <paramref name="maxAge"/>.</summary>
    Task PruneAsync(TimeSpan maxAge, CancellationToken ct = default);
}

/// <summary>A locally cached chat message.</summary>
/// <param name="Id">Server-assigned message ID.</param>
/// <param name="ChannelId">Channel this message belongs to.</param>
/// <param name="SenderName">Display name of the sender.</param>
/// <param name="Content">Plain-text message body.</param>
/// <param name="SentAt">When the message was sent (UTC).</param>
public sealed record CachedMessage(
    Guid Id,
    Guid ChannelId,
    string SenderName,
    string Content,
    DateTimeOffset SentAt);
