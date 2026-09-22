using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Applies the administrator-configured retention policy to chat messages and attachments.
/// </summary>
/// <remarks>
/// <para>
/// A message expires when it is older than <see cref="ChatSettings.MessageLifetimeDays"/> or when
/// it falls outside the newest <see cref="ChatSettings.MaxMessagesPerChannel"/> messages of its
/// channel. Expired messages are then either archived (hidden but retained) or purged
/// (permanently deleted) according to <see cref="ChatSettings.RetentionMode"/>.
/// </para>
/// <para>
/// This service is a singleton and therefore takes its <see cref="ChatDbContext"/> from
/// <see cref="IDbContextFactory{TContext}"/>; it never captures a scoped context. Deletes and
/// updates use bulk operations, so a sweep over a large channel does not load every message
/// into the change tracker.
/// </para>
/// </remarks>
public sealed class ChatRetentionService : IChatRetentionService
{
    /// <summary>
    /// Upper bound on the number of messages expired per channel in a single sweep, so one very
    /// large channel cannot monopolize a sweep. Anything left over is handled by the next sweep.
    /// </summary>
    internal const int MaxMessagesPerChannelPerSweep = 5000;

    /// <summary>Module identifier attached to search-index removal events.</summary>
    private const string SearchModuleId = "chat";

    private readonly IDbContextFactory<ChatDbContext> _dbContextFactory;
    private readonly IChatSettingsProvider _settingsProvider;
    private readonly IEventBus? _eventBus;
    private readonly ILogger<ChatRetentionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatRetentionService"/> class.
    /// </summary>
    /// <param name="dbContextFactory">Factory for short-lived chat contexts.</param>
    /// <param name="settingsProvider">Resolves the admin-configured retention policy.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="eventBus">Optional event bus used to retire expired messages from the search index.</param>
    public ChatRetentionService(
        IDbContextFactory<ChatDbContext> dbContextFactory,
        IChatSettingsProvider settingsProvider,
        ILogger<ChatRetentionService> logger,
        IEventBus? eventBus = null)
    {
        _dbContextFactory = dbContextFactory;
        _settingsProvider = settingsProvider;
        _logger = logger;
        _eventBus = eventBus;
    }

    /// <inheritdoc />
    public async Task<ChatRetentionSweepResult> SweepAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsProvider.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.HasRetentionPolicy)
            return new ChatRetentionSweepResult { Skipped = true };

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var affectedChannelIds = await FindChannelsWithExpiredMessagesAsync(db, settings, cancellationToken)
            .ConfigureAwait(false);

        if (affectedChannelIds.Count == 0)
        {
            _logger.LogDebug("Chat retention sweep found nothing to expire.");
            return new ChatRetentionSweepResult { ChannelsScanned = 0 };
        }

        var result = new ChatRetentionSweepResult { ChannelsScanned = affectedChannelIds.Count };
        var expiredMessageIds = new List<Guid>();

        foreach (var channelId in affectedChannelIds)
        {
            var ids = await CollectExpiredMessageIdsAsync(db, channelId, settings, cancellationToken)
                .ConfigureAwait(false);
            if (ids.Count == 0)
                continue;

            expiredMessageIds.AddRange(ids);

            // Pins reference messages with a RESTRICT foreign key, and a pin pointing at a
            // hidden message would surface as a broken entry, so they are always cleared.
            result = result with
            {
                PinsRemoved = result.PinsRemoved
                    + await db.PinnedMessages
                        .Where(p => ids.Contains(p.MessageId))
                        .ExecuteDeleteAsync(cancellationToken)
                        .ConfigureAwait(false)
            };

            if (settings.RetentionMode == ChatRetentionMode.Purge)
            {
                var purgeOutcome = await PurgeAsync(db, ids, cancellationToken).ConfigureAwait(false);
                result = result with
                {
                    MessagesPurged = result.MessagesPurged + purgeOutcome.MessagesPurged,
                    AttachmentsRemoved = result.AttachmentsRemoved + purgeOutcome.AttachmentsRemoved
                };
            }
            else
            {
                var archiveOutcome = await ArchiveAsync(db, ids, settings, cancellationToken).ConfigureAwait(false);
                result = result with
                {
                    MessagesArchived = result.MessagesArchived + archiveOutcome.MessagesArchived,
                    AttachmentsRemoved = result.AttachmentsRemoved + archiveOutcome.AttachmentsRemoved
                };
            }
        }

        await RetireFromSearchIndexAsync(expiredMessageIds, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Chat retention sweep finished: {Channels} channel(s), {Archived} archived, {Purged} purged, " +
            "{Attachments} attachment(s) removed, {Pins} pin(s) removed (mode: {Mode}).",
            result.ChannelsScanned, result.MessagesArchived, result.MessagesPurged,
            result.AttachmentsRemoved, result.PinsRemoved, settings.RetentionMode);

        return result with { CompletedAtUtc = DateTime.UtcNow };
    }

    /// <summary>
    /// Finds the channels that currently hold at least one expired message, using two aggregate
    /// queries rather than one pass per channel.
    /// </summary>
    private static async Task<List<Guid>> FindChannelsWithExpiredMessagesAsync(
        ChatDbContext db, ChatSettings settings, CancellationToken cancellationToken)
    {
        var channelIds = new HashSet<Guid>();

        if (settings.HasMessageLifetime)
        {
            var cutoff = DateTime.UtcNow.AddDays(-settings.MessageLifetimeDays);
            var aged = await db.Messages
                .Where(m => m.SentAt < cutoff)
                .Select(m => m.ChannelId)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            channelIds.UnionWith(aged);
        }

        if (settings.HasMessageCountLimit)
        {
            var overCap = await db.Messages
                .GroupBy(m => m.ChannelId)
                .Where(g => g.Count() > settings.MaxMessagesPerChannel)
                .Select(g => g.Key)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            channelIds.UnionWith(overCap);
        }

        return [.. channelIds];
    }

    /// <summary>
    /// Collects the identifiers of the messages in one channel that the policy says should expire.
    /// </summary>
    private static async Task<List<Guid>> CollectExpiredMessageIdsAsync(
        ChatDbContext db, Guid channelId, ChatSettings settings, CancellationToken cancellationToken)
    {
        var ids = new HashSet<Guid>();

        if (settings.HasMessageLifetime)
        {
            var cutoff = DateTime.UtcNow.AddDays(-settings.MessageLifetimeDays);
            var aged = await db.Messages
                .Where(m => m.ChannelId == channelId && m.SentAt < cutoff)
                .OrderBy(m => m.SentAt)
                .Select(m => m.Id)
                .Take(MaxMessagesPerChannelPerSweep)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            ids.UnionWith(aged);
        }

        if (settings.HasMessageCountLimit && ids.Count < MaxMessagesPerChannelPerSweep)
        {
            var overCap = await db.Messages
                .Where(m => m.ChannelId == channelId)
                .OrderByDescending(m => m.SentAt)
                .Skip(settings.MaxMessagesPerChannel)
                .Select(m => m.Id)
                .Take(MaxMessagesPerChannelPerSweep)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            ids.UnionWith(overCap);
        }

        return [.. ids];
    }

    /// <summary>Marks the given messages as archived, optionally removing their attachments.</summary>
    private static async Task<(int MessagesArchived, int AttachmentsRemoved)> ArchiveAsync(
        ChatDbContext db, List<Guid> ids, ChatSettings settings, CancellationToken cancellationToken)
    {
        var attachmentsRemoved = 0;
        if (!settings.ArchiveAttachments)
        {
            attachmentsRemoved = await db.MessageAttachments
                .Where(a => ids.Contains(a.MessageId))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var archivedAt = DateTime.UtcNow;
        var archived = await db.Messages
            .IgnoreQueryFilters()
            .Where(m => ids.Contains(m.Id) && m.ArchivedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(m => m.ArchivedAt, archivedAt),
                cancellationToken)
            .ConfigureAwait(false);

        return (archived, attachmentsRemoved);
    }

    /// <summary>Permanently deletes the given messages and the rows that depend on them.</summary>
    private static async Task<(int MessagesPurged, int AttachmentsRemoved)> PurgeAsync(
        ChatDbContext db, List<Guid> ids, CancellationToken cancellationToken)
    {
        // Replies point at their parent with a RESTRICT foreign key; detach them first so the
        // parent can be deleted without losing the replies.
        await db.Messages
            .IgnoreQueryFilters()
            .Where(m => m.ReplyToMessageId != null && ids.Contains(m.ReplyToMessageId.Value))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(m => m.ReplyToMessageId, (Guid?)null),
                cancellationToken)
            .ConfigureAwait(false);

        var attachmentsRemoved = await db.MessageAttachments
            .Where(a => ids.Contains(a.MessageId))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var purged = await db.Messages
            .IgnoreQueryFilters()
            .Where(m => ids.Contains(m.Id))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return (purged, attachmentsRemoved);
    }

    /// <summary>
    /// Publishes search-index removals so expired messages stop appearing in search results.
    /// Best-effort: a failure here must not roll back or fail the sweep.
    /// </summary>
    private async Task RetireFromSearchIndexAsync(IReadOnlyList<Guid> messageIds, CancellationToken cancellationToken)
    {
        if (_eventBus is null || messageIds.Count == 0)
            return;

        var caller = CallerContext.CreateSystemContext();
        foreach (var messageId in messageIds)
        {
            try
            {
                await _eventBus.PublishAsync(new SearchIndexRequestEvent
                {
                    EventId = Guid.CreateVersion7(),
                    CreatedAt = DateTime.UtcNow,
                    ModuleId = SearchModuleId,
                    EntityId = messageId.ToString(),
                    Action = SearchIndexAction.Remove
                }, caller, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to publish search-index removal for expired chat message {MessageId}.", messageId);
            }
        }
    }
}
