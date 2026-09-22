namespace DotNetCloud.UI.Web.Client.Services;

/// <summary>
/// The effective chat limits and retention policy currently enforced by the Chat module,
/// after clamping and defaulting. Served by <c>GET /api/v1/chat/admin/settings/effective</c>.
/// </summary>
public sealed record ChatEffectiveSettingsDto
{
    /// <summary>Maximum number of characters in a message.</summary>
    public int MaxMessageLength { get; init; }

    /// <summary>Maximum live messages per channel (<c>0</c> = unlimited).</summary>
    public int MaxMessagesPerChannel { get; init; }

    /// <summary>Maximum attachments on a single message.</summary>
    public int MaxAttachmentsPerMessage { get; init; }

    /// <summary>Maximum attachments per channel (<c>0</c> = unlimited).</summary>
    public int MaxAttachmentsPerChannel { get; init; }

    /// <summary>Maximum size of a single attachment in megabytes.</summary>
    public int MaxAttachmentSizeMb { get; init; }

    /// <summary>Maximum total attachment storage per channel in megabytes (<c>0</c> = unlimited).</summary>
    public int MaxAttachmentStoragePerChannelMb { get; init; }

    /// <summary>Whether the retention/archiving sweep is enabled.</summary>
    public bool RetentionEnabled { get; init; }

    /// <summary>Age in days after which messages expire (<c>0</c> = keep forever).</summary>
    public int MessageLifetimeDays { get; init; }

    /// <summary>What happens to expired messages: <c>Archive</c> or <c>Purge</c>.</summary>
    public string RetentionMode { get; init; } = "Archive";

    /// <summary>Whether attachments of archived messages are retained.</summary>
    public bool ArchiveAttachments { get; init; }

    /// <summary>How often the retention sweep runs, in minutes.</summary>
    public int SweepIntervalMinutes { get; init; }

    /// <summary>Whether a per-channel message cap is in force.</summary>
    public bool MessageCountLimitActive { get; init; }

    /// <summary>Whether an age-based message lifetime is in force.</summary>
    public bool MessageLifetimeActive { get; init; }

    /// <summary>Whether the retention policy will expire anything at all.</summary>
    public bool RetentionPolicyActive { get; init; }
}

/// <summary>
/// Outcome of a chat retention/archiving sweep. Served by
/// <c>POST /api/v1/chat/admin/retention/sweep</c>.
/// </summary>
public sealed record ChatRetentionSweepResultDto
{
    /// <summary>Number of channels examined.</summary>
    public int ChannelsScanned { get; init; }

    /// <summary>Number of messages archived.</summary>
    public int MessagesArchived { get; init; }

    /// <summary>Number of messages permanently deleted.</summary>
    public int MessagesPurged { get; init; }

    /// <summary>Number of attachment rows removed.</summary>
    public int AttachmentsRemoved { get; init; }

    /// <summary>Number of pins removed because their message expired.</summary>
    public int PinsRemoved { get; init; }

    /// <summary>Whether the sweep did nothing because the retention policy is disabled.</summary>
    public bool Skipped { get; init; }

    /// <summary>UTC time the sweep finished.</summary>
    public DateTime CompletedAtUtc { get; init; }
}
