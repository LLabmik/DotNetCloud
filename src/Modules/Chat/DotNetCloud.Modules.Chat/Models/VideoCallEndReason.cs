namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Defines the reason a video call ended.
/// </summary>
public enum VideoCallEndReason
{
    /// <summary>The call ended normally (participant hung up).</summary>
    Normal,

    /// <summary>The call was rejected by the callee.</summary>
    Rejected,

    /// <summary>The call was missed (no answer within timeout).</summary>
    Missed,

    /// <summary>The call timed out during connection establishment.</summary>
    TimedOut,

    /// <summary>The call failed due to a technical error.</summary>
    Failed,

    /// <summary>The call was cancelled by the initiator before being answered.</summary>
    Cancelled
}
