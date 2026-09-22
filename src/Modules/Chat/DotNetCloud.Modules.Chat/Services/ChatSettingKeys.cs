namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Canonical system-setting keys owned by the Chat module.
/// </summary>
/// <remarks>
/// Values are stored as plain strings in the core <c>SystemSettings</c> table
/// (module <see cref="ModuleId"/>) and edited from the <c>/admin/chat</c> page.
/// The <c>Limits:*</c> keys are enforced at write time; the <c>Retention:*</c> keys
/// drive the background retention/archiving sweep.
/// </remarks>
public static class ChatSettingKeys
{
    /// <summary>Module identifier that owns all Chat system settings.</summary>
    public const string ModuleId = "dotnetcloud.chat";

    /// <summary>Maximum number of characters allowed in a single message (int, 1-10000).</summary>
    public const string MaxMessageLength = "Limits:MaxMessageLength";

    /// <summary>Maximum number of live messages retained per channel; older ones expire (int, 0 = unlimited).</summary>
    public const string MaxMessagesPerChannel = "Limits:MaxMessagesPerChannel";

    /// <summary>Maximum number of attachments allowed on a single message (int, 1-100).</summary>
    public const string MaxAttachmentsPerMessage = "Limits:MaxAttachmentsPerMessage";

    /// <summary>Maximum number of attachments allowed in a channel; new uploads are rejected beyond it (int, 0 = unlimited).</summary>
    public const string MaxAttachmentsPerChannel = "Limits:MaxAttachmentsPerChannel";

    /// <summary>Maximum size in megabytes of a single attachment (int, 1-1024).</summary>
    public const string MaxAttachmentSizeMb = "Limits:MaxAttachmentSizeMb";

    /// <summary>Maximum total attachment storage in megabytes per channel; new uploads are rejected beyond it (int, 0 = unlimited).</summary>
    public const string MaxAttachmentStoragePerChannelMb = "Limits:MaxAttachmentStoragePerChannelMb";

    /// <summary>Master switch for the automatic retention/archiving sweep (bool).</summary>
    public const string RetentionEnabled = "Retention:Enabled";

    /// <summary>Age in days after which messages expire (int, 0 = unlimited).</summary>
    public const string MessageLifetimeDays = "Retention:MessageLifetimeDays";

    /// <summary>What happens to expired messages: <c>Archive</c> (hide, keep) or <c>Purge</c> (permanent delete).</summary>
    public const string RetentionMode = "Retention:Mode";

    /// <summary>Whether attachments of archived messages are retained (bool). When false their records are deleted.</summary>
    public const string ArchiveAttachments = "Retention:ArchiveAttachments";

    /// <summary>How often the retention sweep runs, in minutes (int, 1-1440).</summary>
    public const string SweepIntervalMinutes = "Retention:SweepIntervalMinutes";
}
