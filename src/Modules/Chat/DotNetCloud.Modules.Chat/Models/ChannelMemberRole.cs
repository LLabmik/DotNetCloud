namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Defines the role of a member within a channel.
/// </summary>
public enum ChannelMemberRole
{
    /// <summary>Regular channel member.</summary>
    Member,

    /// <summary>Channel administrator with management permissions.</summary>
    Admin,

    /// <summary>Channel owner with full control.</summary>
    Owner
}
