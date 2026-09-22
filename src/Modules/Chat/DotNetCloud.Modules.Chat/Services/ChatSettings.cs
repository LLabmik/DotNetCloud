namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// What happens to a chat message once it passes the retention policy
/// (message lifetime or per-channel message cap).
/// </summary>
public enum ChatRetentionMode
{
    /// <summary>The message is hidden but retained in the database so it can be restored.</summary>
    Archive,

    /// <summary>The message and its dependent rows are permanently deleted.</summary>
    Purge
}

/// <summary>
/// Administrator-configurable limits and retention policy for the Chat module.
/// </summary>
/// <remarks>
/// Resolved by <see cref="IChatSettingsProvider"/> from the core <c>SystemSettings</c>
/// table (module <c>dotnetcloud.chat</c>), falling back to <c>Chat:*</c> configuration
/// and then to the defaults declared here. A value of <c>0</c> for a count/size limit
/// means "unlimited".
/// </remarks>
public sealed record ChatSettings
{
    /// <summary>Default maximum characters per message.</summary>
    public const int DefaultMaxMessageLength = 10000;

    /// <summary>Default maximum attachments on a single message.</summary>
    public const int DefaultMaxAttachmentsPerMessage = 10;

    /// <summary>Default maximum size in megabytes of a single attachment.</summary>
    public const int DefaultMaxAttachmentSizeMb = 10;

    /// <summary>Default retention sweep interval in minutes.</summary>
    public const int DefaultSweepIntervalMinutes = 60;

    /// <summary>Hard ceiling for <see cref="MaxMessageLength"/> (matches the database column length).</summary>
    public const int HardMaxMessageLength = 10000;

    /// <summary>Hard ceiling for a single attachment, in megabytes (matches the transport limit on chat uploads).</summary>
    public const int HardMaxAttachmentSizeMb = 64;

    /// <summary>Maximum number of characters allowed in a single message.</summary>
    public int MaxMessageLength { get; init; } = DefaultMaxMessageLength;

    /// <summary>Maximum number of live messages retained per channel. <c>0</c> means unlimited.</summary>
    public int MaxMessagesPerChannel { get; init; }

    /// <summary>Maximum number of attachments allowed on a single message.</summary>
    public int MaxAttachmentsPerMessage { get; init; } = DefaultMaxAttachmentsPerMessage;

    /// <summary>Maximum number of attachments allowed per channel. <c>0</c> means unlimited.</summary>
    public int MaxAttachmentsPerChannel { get; init; }

    /// <summary>Maximum size in megabytes of a single attachment.</summary>
    public int MaxAttachmentSizeMb { get; init; } = DefaultMaxAttachmentSizeMb;

    /// <summary>Maximum total attachment storage in megabytes per channel. <c>0</c> means unlimited.</summary>
    public int MaxAttachmentStoragePerChannelMb { get; init; }

    /// <summary>Whether the automatic retention/archiving sweep is enabled.</summary>
    public bool RetentionEnabled { get; init; }

    /// <summary>Age in days after which messages expire. <c>0</c> means unlimited.</summary>
    public int MessageLifetimeDays { get; init; }

    /// <summary>What happens to expired messages.</summary>
    public ChatRetentionMode RetentionMode { get; init; } = ChatRetentionMode.Archive;

    /// <summary>Whether attachments of archived messages are retained; when false their records are deleted.</summary>
    public bool ArchiveAttachments { get; init; } = true;

    /// <summary>How often the retention sweep runs, in minutes.</summary>
    public int SweepIntervalMinutes { get; init; } = DefaultSweepIntervalMinutes;

    /// <summary>Maximum size in bytes of a single attachment.</summary>
    public long MaxAttachmentSizeBytes => MaxAttachmentSizeMb * 1024L * 1024L;

    /// <summary>Maximum total attachment storage in bytes per channel. <c>0</c> means unlimited.</summary>
    public long MaxAttachmentStoragePerChannelBytes => MaxAttachmentStoragePerChannelMb * 1024L * 1024L;

    /// <summary>Whether a single-attachment size ceiling is enforced.</summary>
    public bool HasAttachmentSizeLimit => MaxAttachmentSizeMb > 0;

    /// <summary>Whether a per-channel attachment count ceiling is enforced.</summary>
    public bool HasAttachmentCountLimit => MaxAttachmentsPerChannel > 0;

    /// <summary>Whether a per-channel attachment storage ceiling is enforced.</summary>
    public bool HasAttachmentStorageLimit => MaxAttachmentStoragePerChannelMb > 0;

    /// <summary>Whether a per-channel message count ceiling is enforced.</summary>
    public bool HasMessageCountLimit => MaxMessagesPerChannel > 0;

    /// <summary>Whether an age-based message lifetime is enforced.</summary>
    public bool HasMessageLifetime => MessageLifetimeDays > 0;

    /// <summary>Whether the retention policy would expire anything at all.</summary>
    public bool HasRetentionPolicy => RetentionEnabled && (HasMessageCountLimit || HasMessageLifetime);

    /// <summary>
    /// Clamps every value into its supported range, mapping out-of-range or malformed
    /// input back onto a safe default.
    /// </summary>
    /// <returns>A clamped copy of this instance.</returns>
    public ChatSettings Normalized() => this with
    {
        MaxMessageLength = Math.Clamp(
            MaxMessageLength <= 0 ? DefaultMaxMessageLength : MaxMessageLength, 1, HardMaxMessageLength),
        MaxMessagesPerChannel = Math.Max(0, MaxMessagesPerChannel),
        MaxAttachmentsPerMessage = Math.Clamp(
            MaxAttachmentsPerMessage <= 0 ? DefaultMaxAttachmentsPerMessage : MaxAttachmentsPerMessage, 1, 100),
        MaxAttachmentsPerChannel = Math.Max(0, MaxAttachmentsPerChannel),
        MaxAttachmentSizeMb = Math.Clamp(
            MaxAttachmentSizeMb <= 0 ? DefaultMaxAttachmentSizeMb : MaxAttachmentSizeMb, 1, HardMaxAttachmentSizeMb),
        MaxAttachmentStoragePerChannelMb = Math.Max(0, MaxAttachmentStoragePerChannelMb),
        MessageLifetimeDays = Math.Max(0, MessageLifetimeDays),
        SweepIntervalMinutes = Math.Clamp(
            SweepIntervalMinutes <= 0 ? DefaultSweepIntervalMinutes : SweepIntervalMinutes, 1, 1440)
    };

    /// <summary>
    /// Parses a <see cref="ChatRetentionMode"/> from its stored string form.
    /// </summary>
    /// <param name="value">The stored value (case-insensitive).</param>
    /// <param name="fallback">Fallback used when the value is missing or unrecognized.</param>
    /// <returns>The parsed mode.</returns>
    public static ChatRetentionMode ParseMode(string? value, ChatRetentionMode fallback = ChatRetentionMode.Archive)
        => Enum.TryParse<ChatRetentionMode>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
}
