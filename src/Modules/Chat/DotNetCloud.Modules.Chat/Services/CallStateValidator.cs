using DotNetCloud.Modules.Chat.Models;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Validates and enforces video call state machine transitions.
/// Only valid transitions are allowed; invalid transitions throw <see cref="InvalidOperationException"/>.
/// </summary>
/// <remarks>
/// <para><b>State Machine:</b></para>
/// <code>
/// Ringing → Connecting (answered), Missed (timeout), Rejected, Failed
/// Connecting → Active, Failed
/// Active → Ended, Failed
/// OnHold → Active, Ended, Failed
/// Any terminal state (Ended, Missed, Rejected, Failed) → no transitions allowed
/// </code>
/// </remarks>
public static class CallStateValidator
{
    private static readonly Dictionary<VideoCallState, HashSet<VideoCallState>> ValidTransitions = new()
    {
        [VideoCallState.Ringing] = [VideoCallState.Connecting, VideoCallState.Ended, VideoCallState.Missed, VideoCallState.Rejected, VideoCallState.Failed],
        [VideoCallState.Connecting] = [VideoCallState.Active, VideoCallState.Ended, VideoCallState.Failed],
        [VideoCallState.Active] = [VideoCallState.Ended, VideoCallState.OnHold, VideoCallState.Failed],
        [VideoCallState.OnHold] = [VideoCallState.Active, VideoCallState.Ended, VideoCallState.Failed],
        [VideoCallState.Ended] = [],
        [VideoCallState.Missed] = [],
        [VideoCallState.Rejected] = [],
        [VideoCallState.Failed] = [],
    };

    /// <summary>
    /// Determines whether transitioning from <paramref name="currentState"/> to <paramref name="newState"/> is valid.
    /// </summary>
    /// <param name="currentState">The current state of the call.</param>
    /// <param name="newState">The desired new state.</param>
    /// <returns><c>true</c> if the transition is valid; otherwise <c>false</c>.</returns>
    public static bool IsValidTransition(VideoCallState currentState, VideoCallState newState)
    {
        return ValidTransitions.TryGetValue(currentState, out var allowed) && allowed.Contains(newState);
    }

    /// <summary>
    /// Validates a state transition and throws if it is invalid.
    /// </summary>
    /// <param name="currentState">The current state of the call.</param>
    /// <param name="newState">The desired new state.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the transition from <paramref name="currentState"/> to <paramref name="newState"/> is not allowed.
    /// </exception>
    public static void ValidateTransition(VideoCallState currentState, VideoCallState newState)
    {
        if (!IsValidTransition(currentState, newState))
        {
            throw new InvalidOperationException(
                $"Invalid call state transition from {currentState} to {newState}.");
        }
    }

    /// <summary>
    /// Determines whether the given state is a terminal state (no further transitions allowed).
    /// </summary>
    /// <param name="state">The state to check.</param>
    /// <returns><c>true</c> if the state is terminal; otherwise <c>false</c>.</returns>
    public static bool IsTerminalState(VideoCallState state)
    {
        return state is VideoCallState.Ended
            or VideoCallState.Missed
            or VideoCallState.Rejected
            or VideoCallState.Failed;
    }

    /// <summary>
    /// Gets all valid target states from the given current state.
    /// </summary>
    /// <param name="currentState">The current state of the call.</param>
    /// <returns>A read-only set of states that can be transitioned to.</returns>
    public static IReadOnlySet<VideoCallState> GetValidTargetStates(VideoCallState currentState)
    {
        return ValidTransitions.TryGetValue(currentState, out var allowed)
            ? allowed
            : new HashSet<VideoCallState>();
    }
}
