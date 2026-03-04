namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Defines the type of a chat message.
/// </summary>
public enum MessageType
{
    /// <summary>Regular text message.</summary>
    Text,

    /// <summary>System-generated message (e.g., "User joined the channel").</summary>
    System,

    /// <summary>Message containing a file share.</summary>
    FileShare,

    /// <summary>Reply to another message.</summary>
    Reply
}
