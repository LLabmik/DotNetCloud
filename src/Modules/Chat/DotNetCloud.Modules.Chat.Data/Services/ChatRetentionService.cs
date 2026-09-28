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
/// channel. Expiry itself is delegated to <see cref="IChatMessageExpiryService"/>: in
/// <see cref="ChatRetentionMode.Archive"/> mode the message is written to the configured archive
/// directory first and its rows are deleted only afterwards; in <see cref="ChatRetentionMode.Purge"/> mode the rows
/// are deleted outright.
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
    private readonly IChatMessageExpiryService _expiryService;
    private readonly IEventBus? _eventBus;
    private readonly ILogger<ChatRetentionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatRetentionService"/> class.
    /// </summary>
    /// <param name="dbContextFactory">Factory for short-lived chat contexts.</param>
    /// <param name="settingsProvider">Resolves the admin-configured retention policy.</param>
    /// <param name="expiryService">Applies the archive-or-purge action to the expired messages.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="eventBus">Optional event bus used to retire expired messages from the search index.</param>
    public ChatRetentionService(
        IDbContextFactory<ChatDbContext> dbContextFactory,
        IChatSettingsProvider settingsProvider,
        IChatMessageExpiryService expiryService,
        ILogger<ChatRetentionService> logger,
        IEventBus? eventBus = null)
    {
        _dbContextFactory = dbContextFactory;
        _settingsProvider = settingsProvider;
        _expiryService = expiryService;
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
        var removedMessageIds = new List<Guid>();

        foreach (var channelId in affectedChannelIds)
        {
            var ids = await CollectExpiredMessageIdsAsync(db, channelId, settings, cancellationToken)
                .ConfigureAwait(false);
            if (ids.Count == 0)
                continue;

            var outcome = await _expiryService.ExpireAsync(db, ids, settings, cancellationToken)
                .ConfigureAwait(false);

            // Only messages that were actually removed may leave the search index: in Archive mode
            // a failed export keeps the message, so it must stay searchable.
            removedMessageIds.AddRange(outcome.RemovedMessageIds);

            result = result with
            {
                MessagesArchived = settings.RetentionMode == ChatRetentionMode.Archive
                    ? result.MessagesArchived + outcome.RemovedMessageIds.Count
                    : result.MessagesArchived,
                MessagesPurged = settings.RetentionMode == ChatRetentionMode.Purge
                    ? result.MessagesPurged + outcome.RemovedMessageIds.Count
                    : result.MessagesPurged,
                AttachmentsRemoved = result.AttachmentsRemoved + outcome.AttachmentsRemoved,
                AttachmentsArchived = result.AttachmentsArchived + outcome.AttachmentsArchived,
                MessagesSkipped = result.MessagesSkipped + outcome.MessagesSkipped,
                PinsRemoved = result.PinsRemoved + outcome.PinsRemoved
            };
        }

        await RetireFromSearchIndexAsync(removedMessageIds, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Chat retention sweep finished: {Channels} channel(s), {Archived} archived, {Purged} purged, " +
            "{Attachments} attachment row(s) removed, {ArchivedAttachments} attachment payload(s) archived, " +
            "{Skipped} message(s) kept, {Pins} pin(s) removed (mode: {Mode}).",
            result.ChannelsScanned, result.MessagesArchived, result.MessagesPurged,
            result.AttachmentsRemoved, result.AttachmentsArchived, result.MessagesSkipped,
            result.PinsRemoved, settings.RetentionMode);

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

        if (settings.HasAttachmentCap)
        {
            channelIds.UnionWith(await FindChannelsOverAttachmentCapAsync(db, settings, cancellationToken)
                .ConfigureAwait(false));
        }

        return [.. channelIds];
    }

    /// <summary>
    /// Finds the channels whose live attachments exceed the configured per-channel count or total
    /// storage ceiling.
    /// </summary>
    private static async Task<List<Guid>> FindChannelsOverAttachmentCapAsync(
        ChatDbContext db, ChatSettings settings, CancellationToken cancellationToken)
    {
        var channelIds = new HashSet<Guid>();

        if (settings.HasAttachmentCountLimit)
        {
            var overCount = await db.MessageAttachments
                .GroupBy(a => a.Message!.ChannelId)
                .Where(g => g.Count() > settings.MaxAttachmentsPerChannel)
                .Select(g => g.Key)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            channelIds.UnionWith(overCount);
        }

        if (settings.HasAttachmentStorageLimit)
        {
            var overStorage = await db.MessageAttachments
                .GroupBy(a => a.Message!.ChannelId)
                .Where(g => g.Sum(a => (long)a.FileSize) > settings.MaxAttachmentStoragePerChannelBytes)
                .Select(g => g.Key)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            channelIds.UnionWith(overStorage);
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

        if (settings.HasAttachmentCap && ids.Count < MaxMessagesPerChannelPerSweep)
        {
            var overAttachments = await CollectAttachmentCapOverflowAsync(db, channelId, settings, ids, cancellationToken)
                .ConfigureAwait(false);

            ids.UnionWith(overAttachments);
        }

        return [.. ids];
    }

    /// <summary>
    /// Selects the oldest messages that carry attachments until removing them would bring the
    /// channel back under its attachment count/storage ceiling. Mirrors the message-count cap: the
    /// channel stays writable and its oldest content expires instead of new uploads being rejected.
    /// </summary>
    private static async Task<List<Guid>> CollectAttachmentCapOverflowAsync(
        ChatDbContext db,
        Guid channelId,
        ChatSettings settings,
        IReadOnlyCollection<Guid> alreadyExpired,
        CancellationToken cancellationToken)
    {
        var liveAttachments = db.MessageAttachments.Where(a => a.Message!.ChannelId == channelId);

        var totalCount = await liveAttachments.CountAsync(cancellationToken).ConfigureAwait(false);
        var totalBytes = await liveAttachments.SumAsync(a => (long?)a.FileSize, cancellationToken).ConfigureAwait(false) ?? 0;

        // Messages already selected by the lifetime/message-count rules are about to go anyway, so
        // their attachments must not count towards the overflow.
        if (alreadyExpired.Count > 0)
        {
            var expiring = db.MessageAttachments.Where(a => alreadyExpired.Contains(a.MessageId));
            totalCount -= await expiring.CountAsync(cancellationToken).ConfigureAwait(false);
            totalBytes -= await expiring.SumAsync(a => (long?)a.FileSize, cancellationToken).ConfigureAwait(false) ?? 0;
        }

        var overCount = settings.HasAttachmentCountLimit
            ? Math.Max(0, totalCount - settings.MaxAttachmentsPerChannel)
            : 0;
        var overBytes = settings.HasAttachmentStorageLimit
            ? Math.Max(0, totalBytes - settings.MaxAttachmentStoragePerChannelBytes)
            : 0;

        if (overCount == 0 && overBytes == 0)
            return [];

        var candidates = await db.Messages
            .Where(m => m.ChannelId == channelId && !alreadyExpired.Contains(m.Id) && m.Attachments.Count > 0)
            .OrderBy(m => m.SentAt)
            .Select(m => new
            {
                m.Id,
                Count = m.Attachments.Count,
                Bytes = m.Attachments.Sum(a => (long)a.FileSize)
            })
            .Take(MaxMessagesPerChannelPerSweep)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var selected = new List<Guid>();
        foreach (var candidate in candidates)
        {
            if (overCount <= 0 && overBytes <= 0)
                break;

            selected.Add(candidate.Id);
            overCount -= candidate.Count;
            overBytes -= candidate.Bytes;
        }

        return selected;
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
