using System.Globalization;
using System.Text.Json;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>One archived message as written to the archive directory.</summary>
public sealed record ArchivedMessageRecord
{
    /// <summary>Identifier of the archived message.</summary>
    public required Guid MessageId { get; init; }

    /// <summary>Channel the message belonged to.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>User who sent the message.</summary>
    public required Guid SenderUserId { get; init; }

    /// <summary>When the message was sent (UTC).</summary>
    public required DateTime SentAt { get; init; }

    /// <summary>When the message was last edited (UTC), if ever.</summary>
    public DateTime? EditedAt { get; init; }

    /// <summary>Message this one replied to, if any.</summary>
    public Guid? ReplyToMessageId { get; init; }

    /// <summary>Message body (Markdown).</summary>
    public required string Content { get; init; }

    /// <summary>Message type name.</summary>
    public required string MessageType { get; init; }

    /// <summary>When this archive record was written (UTC).</summary>
    public required DateTime ArchivedAtUtc { get; init; }

    /// <summary>Attachments recorded with the message.</summary>
    public IReadOnlyList<ArchivedAttachmentRecord> Attachments { get; init; } = [];
}

/// <summary>Attachment metadata as written to the archive directory.</summary>
public sealed record ArchivedAttachmentRecord
{
    /// <summary>Display file name.</summary>
    public required string FileName { get; init; }

    /// <summary>MIME type.</summary>
    public required string MimeType { get; init; }

    /// <summary>Size in bytes.</summary>
    public long FileSize { get; init; }

    /// <summary>
    /// File node in the Files module when the attachment is a Files reference. The bytes stay in
    /// the Files module and are not copied into the archive.
    /// </summary>
    public Guid? FileNodeId { get; init; }

    /// <summary>Original thumbnail/serving URL, when one was recorded.</summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>
    /// Path of the copied payload relative to the message's archive folder. Set only when the
    /// Chat module owned the bytes (inline uploads).
    /// </summary>
    public string? ArchivedFile { get; init; }
}

/// <summary>Outcome of one archive export pass.</summary>
public sealed record ChatArchiveExportOutcome
{
    /// <summary>Number of messages written to the archive.</summary>
    public int MessagesExported { get; init; }

    /// <summary>Number of attachment payloads copied into the archive.</summary>
    public int AttachmentsExported { get; init; }

    /// <summary>Messages that could not be exported; their rows must be left untouched.</summary>
    public IReadOnlyList<Guid> FailedMessageIds { get; init; } = [];

    /// <summary>Set when the archive root itself could not be created, so nothing was exported.</summary>
    public bool ArchiveRootUnavailable { get; init; }
}

/// <summary>
/// Writes expired messages — and the attachment payloads the Chat module owns — to the configured
/// archive directory so their rows can safely be deleted afterwards.
/// </summary>
public interface IChatArchiveExporter
{
    /// <summary>Resolves the directory this exporter writes to for the given settings.</summary>
    /// <param name="settings">Resolved chat settings.</param>
    string ResolveArchiveRoot(ChatSettings settings);

    /// <summary>
    /// Writes the given messages to the archive. A message is only reported as exported once its
    /// JSON record has been written; any failure leaves it out of the result so the caller keeps
    /// its rows.
    /// </summary>
    /// <param name="db">Chat context used to read the messages being exported.</param>
    /// <param name="messageIds">Messages to archive.</param>
    /// <param name="settings">Resolved chat settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ChatArchiveExportOutcome> ExportAsync(
        ChatDbContext db,
        IReadOnlyList<Guid> messageIds,
        ChatSettings settings,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IChatArchiveExporter"/>: one JSON record per message plus a copy of every
/// chat-owned attachment payload.
/// </summary>
/// <remarks>
/// Archive layout (year folder, then month folder):
/// <c>{root}/{yyyy}/{MM}/{messageId}.json</c> with attachment payloads under
/// <c>{root}/{yyyy}/{MM}/{messageId}/{fileName}</c>. The channel stays available in each JSON
/// record. The JSON record is written last so its presence means the export completed; a crash
/// mid-copy leaves an orphaned file but never a half-exported record.
/// </remarks>
internal sealed class ChatArchiveExporter : IChatArchiveExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatArchiveExporter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatArchiveExporter"/> class.
    /// </summary>
    /// <param name="configuration">Configuration used to locate chat-owned uploads.</param>
    /// <param name="logger">Logger instance.</param>
    public ChatArchiveExporter(IConfiguration configuration, ILogger<ChatArchiveExporter> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ResolveArchiveRoot(ChatSettings settings)
        => ChatArchivePathResolver.Resolve(
            settings.ArchivePath,
            Environment.GetEnvironmentVariable(ChatUploadPathResolver.DataDirEnvironmentVariable));

    /// <inheritdoc />
    public async Task<ChatArchiveExportOutcome> ExportAsync(
        ChatDbContext db,
        IReadOnlyList<Guid> messageIds,
        ChatSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (messageIds.Count == 0)
        {
            return new ChatArchiveExportOutcome();
        }

        var archiveRoot = ResolveArchiveRoot(settings);
        try
        {
            Directory.CreateDirectory(archiveRoot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Chat archive directory {ArchiveRoot} could not be created; no messages were archived.",
                archiveRoot);
            return new ChatArchiveExportOutcome { ArchiveRootUnavailable = true };
        }

        var messages = await db.Messages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(m => m.Attachments)
            .Where(m => messageIds.Contains(m.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var uploadsDirectory = ChatUploadPathResolver.ResolveUploadsDirectory(
            _configuration,
            Environment.GetEnvironmentVariable(ChatUploadPathResolver.DataDirEnvironmentVariable));

        var exported = 0;
        var attachmentsExported = 0;
        var failed = new List<Guid>();
        var archivedAtUtc = DateTime.UtcNow;

        foreach (var message in messages)
        {
            try
            {
                var messageFolderName = message.Id.ToString("N");
                var folder = Path.Combine(
                    archiveRoot,
                    message.SentAt.ToString("yyyy", CultureInfo.InvariantCulture),
                    message.SentAt.ToString("MM", CultureInfo.InvariantCulture));

                var payload = await WriteMessageAsync(
                    message, folder, messageFolderName, uploadsDirectory, settings, archivedAtUtc, cancellationToken)
                    .ConfigureAwait(false);

                attachmentsExported += payload;
                exported++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to archive chat message {MessageId}; its rows were kept.", message.Id);
                failed.Add(message.Id);
            }
        }

        return new ChatArchiveExportOutcome
        {
            MessagesExported = exported,
            AttachmentsExported = attachmentsExported,
            FailedMessageIds = failed
        };
    }

    /// <summary>Copies the message's chat-owned attachments, then writes its JSON record.</summary>
    /// <returns>Number of attachment payloads copied.</returns>
    private async Task<int> WriteMessageAsync(
        Models.Message message,
        string folder,
        string messageFolderName,
        string uploadsDirectory,
        ChatSettings settings,
        DateTime archivedAtUtc,
        CancellationToken cancellationToken)
    {
        var attachmentRecords = new List<ArchivedAttachmentRecord>();
        var copied = 0;

        if (settings.ArchiveAttachments)
        {
            Directory.CreateDirectory(folder);
            var payloadFolder = Path.Combine(folder, messageFolderName);

            foreach (var attachment in message.Attachments)
            {
                string? archivedFile = null;
                var storedName = TryGetStoredUploadName(attachment.ThumbnailUrl);

                if (storedName is not null)
                {
                    var source = Path.Combine(uploadsDirectory, storedName);
                    if (File.Exists(source))
                    {
                        Directory.CreateDirectory(payloadFolder);
                        var target = Path.Combine(payloadFolder, SanitizeFileName(attachment.FileName, storedName));
                        File.Copy(source, target, overwrite: true);
                        archivedFile = Path.Combine(messageFolderName, Path.GetFileName(target)).Replace('\\', '/');
                        copied++;
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Archived attachment payload {StoredName} for message {MessageId} was not found on disk.",
                            storedName, message.Id);
                    }
                }

                attachmentRecords.Add(new ArchivedAttachmentRecord
                {
                    FileName = attachment.FileName,
                    MimeType = attachment.MimeType,
                    FileSize = attachment.FileSize,
                    FileNodeId = attachment.FileNodeId,
                    ThumbnailUrl = attachment.ThumbnailUrl,
                    ArchivedFile = archivedFile
                });
            }
        }

        Directory.CreateDirectory(folder);

        var record = new ArchivedMessageRecord
        {
            MessageId = message.Id,
            ChannelId = message.ChannelId,
            SenderUserId = message.SenderUserId,
            SentAt = message.SentAt,
            EditedAt = message.EditedAt,
            ReplyToMessageId = message.ReplyToMessageId,
            Content = message.Content,
            MessageType = message.Type.ToString(),
            ArchivedAtUtc = archivedAtUtc,
            Attachments = attachmentRecords
        };

        // Written last: its presence means the record (and any payloads) completed.
        await File.WriteAllTextAsync(
            Path.Combine(folder, $"{messageFolderName}.json"),
            JsonSerializer.Serialize(record, JsonOptions),
            cancellationToken).ConfigureAwait(false);

        return copied;
    }

    /// <summary>
    /// Extracts the stored upload file name from a chat upload URL, or <see langword="null"/> when
    /// the URL does not point at a chat-owned upload (for example a Files-module reference).
    /// </summary>
    internal static string? TryGetStoredUploadName(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var marker = ChatUploadPathResolver.UploadUrlPrefix;
        var index = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var name = url[(index + marker.Length)..];
        var queryIndex = name.IndexOfAny(['?', '#']);
        if (queryIndex >= 0)
        {
            name = name[..queryIndex];
        }

        // Reject anything that could escape the uploads directory.
        if (name.Length == 0
            || name.Contains('/')
            || name.Contains('\\')
            || name.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        return name;
    }

    /// <summary>Keeps a display file name safe to use as a path segment.</summary>
    private static string SanitizeFileName(string fileName, string fallback)
    {
        var name = string.IsNullOrWhiteSpace(fileName) ? fallback : Path.GetFileName(fileName.Trim());
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }
}
