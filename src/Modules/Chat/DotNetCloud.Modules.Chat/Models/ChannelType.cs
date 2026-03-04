namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Defines the type of a chat channel.
/// </summary>
public enum ChannelType
{
    /// <summary>A public channel visible to all organization members.</summary>
    Public,

    /// <summary>A private channel visible only to invited members.</summary>
    Private,

    /// <summary>A one-to-one direct message channel.</summary>
    DirectMessage,

    /// <summary>A group direct message channel with multiple users.</summary>
    Group
}
