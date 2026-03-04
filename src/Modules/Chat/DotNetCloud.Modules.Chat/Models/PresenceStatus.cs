namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// User presence status for chat.
/// </summary>
public enum PresenceStatus
{
    /// <summary>User is actively online.</summary>
    Online,

    /// <summary>User is away / idle.</summary>
    Away,

    /// <summary>User has enabled do-not-disturb.</summary>
    DoNotDisturb,

    /// <summary>User is offline.</summary>
    Offline
}
