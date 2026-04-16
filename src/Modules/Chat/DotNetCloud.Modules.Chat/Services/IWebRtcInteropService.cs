using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Provides C# interop with the client-side WebRTC engine (video-call.js).
/// Wraps browser WebRTC API calls for Blazor components.
/// </summary>
public interface IWebRtcInteropService
{
    /// <summary>
    /// Initialize the WebRTC engine with ICE server config and Blazor callback reference.
    /// </summary>
    /// <param name="dotNetRef">Blazor DotNetObjectReference for JS → .NET callbacks.</param>
    /// <param name="config">WebRTC call configuration including ICE servers.</param>
    /// <returns>True if initialization succeeded.</returns>
    Task<bool> InitializeCallAsync<T>(DotNetObjectReference<T> dotNetRef, WebRtcCallConfig config) where T : class;

    /// <summary>
    /// Start capturing local media (camera + microphone).
    /// </summary>
    /// <returns>The local stream ID, or null on failure.</returns>
    Task<string?> StartLocalMediaAsync();

    /// <summary>
    /// Start screen sharing via getDisplayMedia.
    /// </summary>
    /// <returns>The screen stream ID, or null on failure.</returns>
    Task<string?> StartScreenShareAsync();

    /// <summary>
    /// Stop screen sharing and revert to camera.
    /// </summary>
    Task StopScreenShareAsync();

    /// <summary>
    /// Create a peer connection and generate an SDP offer.
    /// </summary>
    /// <param name="peerId">The remote user ID.</param>
    /// <returns>The SDP offer JSON string, or null on failure.</returns>
    Task<string?> CreateOfferAsync(string peerId);

    /// <summary>
    /// Handle an incoming SDP offer and create an answer.
    /// </summary>
    /// <param name="peerId">The remote user ID.</param>
    /// <param name="sdpJson">The SDP offer JSON.</param>
    /// <returns>The SDP answer JSON string, or null on failure.</returns>
    Task<string?> HandleOfferAsync(string peerId, string sdpJson);

    /// <summary>
    /// Handle an incoming SDP answer.
    /// </summary>
    /// <param name="peerId">The remote user ID.</param>
    /// <param name="sdpJson">The SDP answer JSON.</param>
    /// <returns>True on success.</returns>
    Task<bool> HandleAnswerAsync(string peerId, string sdpJson);

    /// <summary>
    /// Add an ICE candidate received from a remote peer.
    /// </summary>
    /// <param name="peerId">The remote user ID.</param>
    /// <param name="candidateJson">The ICE candidate JSON.</param>
    /// <returns>True on success.</returns>
    Task<bool> AddIceCandidateAsync(string peerId, string candidateJson);

    /// <summary>
    /// Toggle local audio track.
    /// </summary>
    /// <param name="enabled">True to unmute, false to mute.</param>
    Task ToggleAudioAsync(bool enabled);

    /// <summary>
    /// Toggle local video track.
    /// </summary>
    /// <param name="enabled">True to enable, false to disable.</param>
    Task ToggleVideoAsync(bool enabled);

    /// <summary>
    /// Attach a media stream to an HTML video element.
    /// </summary>
    /// <param name="elementId">The DOM element ID.</param>
    /// <param name="streamType">"local", "screen", or a peer user ID.</param>
    /// <returns>True if attached.</returns>
    Task<bool> AttachStreamToElementAsync(string elementId, string streamType);

    /// <summary>
    /// Detach a stream from an HTML video element.
    /// </summary>
    /// <param name="elementId">The DOM element ID.</param>
    Task DetachStreamFromElementAsync(string elementId);

    /// <summary>
    /// Close a specific peer connection.
    /// </summary>
    /// <param name="peerId">The remote user ID.</param>
    Task ClosePeerConnectionAsync(string peerId);

    /// <summary>
    /// Hang up the call: close all connections, stop all tracks.
    /// </summary>
    Task HangupAsync();

    /// <summary>
    /// Full disposal: hangup + release Blazor reference.
    /// </summary>
    Task DisposeAsync();

    /// <summary>
    /// Get the current call state from the JS engine.
    /// </summary>
    Task<WebRtcCallState?> GetCallStateAsync();

    /// <summary>
    /// Get a specific peer's connection state.
    /// </summary>
    /// <param name="peerId">The remote user ID.</param>
    Task<WebRtcPeerState?> GetPeerStateAsync(string peerId);

    /// <summary>
    /// Get local media track states.
    /// </summary>
    Task<WebRtcMediaState?> GetMediaStateAsync();
}
