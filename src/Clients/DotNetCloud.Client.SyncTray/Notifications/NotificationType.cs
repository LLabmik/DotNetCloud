namespace DotNetCloud.Client.SyncTray.Notifications;

/// <summary>Classifies a user-visible notification by its severity / intent.</summary>
public enum NotificationType
{
    /// <summary>Informational — sync completed, etc.</summary>
    Info,

    /// <summary>New chat message notification.</summary>
    Chat,

    /// <summary>Chat message containing a mention for the current user.</summary>
    Mention,

    /// <summary>Non-fatal warning — quota approaching limit, etc.</summary>
    Warning,

    /// <summary>Error — sync failed, disk full, etc.</summary>
    Error,
}
