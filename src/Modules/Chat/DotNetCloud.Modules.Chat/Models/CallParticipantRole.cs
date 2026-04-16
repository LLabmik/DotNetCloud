namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Defines the role of a participant in a video call.
/// </summary>
public enum CallParticipantRole
{
    /// <summary>The user who initiated the call.</summary>
    Initiator,

    /// <summary>A user who joined an existing call.</summary>
    Participant
}
