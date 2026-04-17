namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Defines the role of a participant in a video call.
/// </summary>
public enum CallParticipantRole
{
    /// <summary>The current host of the call (has control authority).</summary>
    Host,

    /// <summary>A user who joined an existing call.</summary>
    Participant
}
