using DotNetCloud.Core.Authorization;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Service for managing WebRTC signaling operations between call participants.
/// </summary>
public interface ICallSignalingService
{
    /// <summary>Sends an SDP offer to a target participant.</summary>
    /// <param name="callId">The video call ID.</param>
    /// <param name="targetUserId">The user to send the offer to.</param>
    /// <param name="sdpOffer">The SDP offer payload.</param>
    /// <param name="caller">The caller context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendOfferAsync(Guid callId, Guid targetUserId, string sdpOffer, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Sends an SDP answer to a target participant.</summary>
    /// <param name="callId">The video call ID.</param>
    /// <param name="targetUserId">The user to send the answer to.</param>
    /// <param name="sdpAnswer">The SDP answer payload.</param>
    /// <param name="caller">The caller context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAnswerAsync(Guid callId, Guid targetUserId, string sdpAnswer, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Sends an ICE candidate to a target participant.</summary>
    /// <param name="callId">The video call ID.</param>
    /// <param name="targetUserId">The user to send the candidate to.</param>
    /// <param name="iceCandidate">The ICE candidate payload.</param>
    /// <param name="caller">The caller context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendIceCandidateAsync(Guid callId, Guid targetUserId, string iceCandidate, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Sends a media state change notification to call participants.</summary>
    /// <param name="callId">The video call ID.</param>
    /// <param name="mediaType">The type of media that changed (Audio, Video, ScreenShare).</param>
    /// <param name="enabled">Whether the media is now enabled or disabled.</param>
    /// <param name="caller">The caller context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendMediaStateChangeAsync(Guid callId, string mediaType, bool enabled, CallerContext caller, CancellationToken cancellationToken = default);
}
