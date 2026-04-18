namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Represents the current state of a call participant within a video call lifecycle.
/// </summary>
public enum ParticipantState
{
    /// <summary>The participant has been invited but has not yet joined the call.</summary>
    Invited,

    /// <summary>The participant has joined and is currently active in the call.</summary>
    Joined,

    /// <summary>The participant has left the call.</summary>
    Left,

    /// <summary>The participant rejected the call invitation.</summary>
    Rejected
}
