using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.DTOs;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Service for managing video call lifecycle operations.
/// </summary>
public interface IVideoCallService
{
    /// <summary>Initiates a new video call in a channel.</summary>
    /// <param name="channelId">The channel to start the call in.</param>
    /// <param name="request">Call initiation parameters.</param>
    /// <param name="caller">The caller context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created video call.</returns>
    Task<VideoCallDto> InitiateCallAsync(Guid channelId, StartCallRequest request, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Joins an active video call.</summary>
    /// <param name="callId">The call to join.</param>
    /// <param name="request">Join parameters.</param>
    /// <param name="caller">The caller context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated video call.</returns>
    Task<VideoCallDto> JoinCallAsync(Guid callId, JoinCallRequest request, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Leaves an active video call.</summary>
    /// <param name="callId">The call to leave.</param>
    /// <param name="caller">The caller context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LeaveCallAsync(Guid callId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Ends a video call for all participants.</summary>
    /// <param name="callId">The call to end.</param>
    /// <param name="caller">The caller context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EndCallAsync(Guid callId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Rejects an incoming video call.</summary>
    /// <param name="callId">The call to reject.</param>
    /// <param name="caller">The caller context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RejectCallAsync(Guid callId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets paginated call history for a channel.</summary>
    /// <param name="channelId">The channel to get history for.</param>
    /// <param name="skip">Number of records to skip.</param>
    /// <param name="take">Number of records to return.</param>
    /// <param name="caller">The caller context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of call history entries.</returns>
    Task<IReadOnlyList<CallHistoryDto>> GetCallHistoryAsync(Guid channelId, int skip, int take, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets the currently active call for a channel, if any.</summary>
    /// <param name="channelId">The channel to check.</param>
    /// <param name="caller">The caller context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active video call, or null if no call is active.</returns>
    Task<VideoCallDto?> GetActiveCallAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a specific video call by its ID.</summary>
    /// <param name="callId">The video call ID.</param>
    /// <param name="caller">The caller context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The video call, or null if not found.</returns>
    Task<VideoCallDto?> GetCallByIdAsync(Guid callId, CallerContext caller, CancellationToken cancellationToken = default);
}
