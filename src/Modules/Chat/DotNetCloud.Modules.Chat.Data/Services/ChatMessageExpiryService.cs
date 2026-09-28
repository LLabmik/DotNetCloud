using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>Result of expiring a set of messages.</summary>
public sealed record ChatExpiryOutcome
{
    /// <summary>Messages whose rows were removed (archived-then-deleted, or purged).</summary>
    public IReadOnlyList<Guid> RemovedMessageIds { get; init; } = [];

    /// <summary>Pins removed because they pointed at an expired message.</summary>
    public int PinsRemoved { get; init; }

    /// <summary>Attachment rows removed with their message.</summary>
    public int AttachmentsRemoved { get; init; }

    /// <summary>Attachment payloads written to the archive directory (Archive mode only).</summary>
    public int AttachmentsArchived { get; init; }

    /// <summary>
    /// Messages kept because their archive export failed. Only non-zero in Archive mode, where a
    /// message may only be deleted once it has been written to disk.
    /// </summary>
    public int MessagesSkipped { get; init; }
}

/// <summary>
/// Applies the configured retention action to a specific set of messages.
/// </summary>
/// <remarks>
/// Shared by the background retention sweep and by write-time enforcement (a channel that exceeds
/// its attachment budget expires its oldest attachments instead of rejecting the write).
/// <para>
/// <b>Archive</b> writes each message to the configured archive directory and then deletes its
/// rows, so the disk copy is the archive of record. A message whose export fails is left untouched
/// — data is never deleted before it has been written.
/// </para>
/// <para>
/// <b>Purge</b> deletes the messages and their dependent rows outright.
/// </para>
/// </remarks>
public interface IChatMessageExpiryService
{
    /// <summary>
    /// Expires the given messages according to <see cref="ChatSettings.RetentionMode"/>.
    /// </summary>
    /// <param name="db">Chat context the caller owns; bulk operations run through it.</param>
    /// <param name="messageIds">Messages to expire.</param>
    /// <param name="settings">Resolved chat settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ChatExpiryOutcome> ExpireAsync(
        ChatDbContext db,
        IReadOnlyList<Guid> messageIds,
        ChatSettings settings,
        CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IChatMessageExpiryService"/> implementation.</summary>
internal sealed class ChatMessageExpiryService : IChatMessageExpiryService
{
    private readonly IChatArchiveExporter _archiveExporter;
    private readonly ILogger<ChatMessageExpiryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatMessageExpiryService"/> class.
    /// </summary>
    /// <param name="archiveExporter">Writes archived messages to disk before their rows go.</param>
    /// <param name="logger">Logger instance.</param>
    public ChatMessageExpiryService(IChatArchiveExporter archiveExporter, ILogger<ChatMessageExpiryService> logger)
    {
        _archiveExporter = archiveExporter;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ChatExpiryOutcome> ExpireAsync(
        ChatDbContext db,
        IReadOnlyList<Guid> messageIds,
        ChatSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (messageIds.Count == 0)
        {
            return new ChatExpiryOutcome();
        }

        // Pins reference messages with a RESTRICT foreign key, and a pin pointing at a message
        // that no longer exists would surface as a broken entry, so they are always cleared.
        var pinsRemoved = await db.PinnedMessages
            .Where(p => messageIds.Contains(p.MessageId))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<Guid> deletableIds = messageIds;
        var attachmentsArchived = 0;
        var messagesSkipped = 0;

        if (settings.RetentionMode == ChatRetentionMode.Archive)
        {
            var export = await _archiveExporter.ExportAsync(db, messageIds, settings, cancellationToken)
                .ConfigureAwait(false);

            attachmentsArchived = export.AttachmentsExported;

            if (export.ArchiveRootUnavailable)
            {
                // Nothing was written at all, so nothing may be deleted. Without this the export
                // outcome (no per-message failures) would look like a clean run and every row
                // would be removed with no archive copy.
                messagesSkipped = messageIds.Count;
                deletableIds = [];

                _logger.LogError(
                    "Chat archive directory is unavailable; kept all {Count} expired message(s) instead of " +
                    "deleting them without an archive copy.",
                    messageIds.Count);
            }
            else if (export.FailedMessageIds.Count > 0)
            {
                messagesSkipped = export.FailedMessageIds.Count;

                var failed = export.FailedMessageIds.ToHashSet();
                deletableIds = [.. messageIds.Where(id => !failed.Contains(id))];

                _logger.LogWarning(
                    "Chat archive export failed for {Skipped} of {Total} message(s); their rows were kept " +
                    "so the data is not lost. They will be retried on the next sweep.",
                    messagesSkipped, messageIds.Count);
            }
        }

        if (deletableIds.Count == 0)
        {
            return new ChatExpiryOutcome { PinsRemoved = pinsRemoved, MessagesSkipped = messagesSkipped };
        }

        // Replies point at their parent with a RESTRICT foreign key; detach them first so the
        // parent can be deleted without losing the replies.
        await db.Messages
            .IgnoreQueryFilters()
            .Where(m => m.ReplyToMessageId != null && deletableIds.Contains(m.ReplyToMessageId.Value))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(m => m.ReplyToMessageId, (Guid?)null),
                cancellationToken)
            .ConfigureAwait(false);

        var attachmentsRemoved = await db.MessageAttachments
            .Where(a => deletableIds.Contains(a.MessageId))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var removed = await db.Messages
            .IgnoreQueryFilters()
            .Where(m => deletableIds.Contains(m.Id))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ChatExpiryOutcome
        {
            RemovedMessageIds = deletableIds,
            PinsRemoved = pinsRemoved,
            AttachmentsRemoved = attachmentsRemoved,
            AttachmentsArchived = attachmentsArchived,
            MessagesSkipped = messagesSkipped
        };
    }
}
