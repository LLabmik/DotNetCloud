namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Defines the state of a video call throughout its lifecycle.
/// </summary>
public enum VideoCallState
{
    /// <summary>The call has been initiated and is ringing for participants.</summary>
    Ringing,

    /// <summary>A participant has answered and the connection is being established.</summary>
    Connecting,

    /// <summary>The call is active with connected participants.</summary>
    Active,

    /// <summary>The call is temporarily on hold.</summary>
    OnHold,

    /// <summary>The call ended normally.</summary>
    Ended,

    /// <summary>The call was not answered within the ring timeout period.</summary>
    Missed,

    /// <summary>The call was explicitly rejected by the callee.</summary>
    Rejected,

    /// <summary>The call failed due to an error.</summary>
    Failed
}
