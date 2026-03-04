namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Defines the type of an @mention in a message.
/// </summary>
public enum MentionType
{
    /// <summary>Mention of a specific user (@username).</summary>
    User,

    /// <summary>Mention of a channel (@channel).</summary>
    Channel,

    /// <summary>Mention of everyone (@all).</summary>
    All
}
