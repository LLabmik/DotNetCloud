namespace DotNetCloud.Client.SyncTray.Notifications;

/// <summary>Classifies a user-visible notification by its severity / intent.</summary>
public enum NotificationType
{
    /// <summary>Informational — sync completed, etc.</summary>
    Info,

    /// <summary>Non-fatal warning — quota approaching limit, etc.</summary>
    Warning,

    /// <summary>Error — sync failed, disk full, etc.</summary>
    Error,
}
