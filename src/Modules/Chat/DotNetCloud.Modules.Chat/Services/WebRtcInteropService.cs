using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// C# interop service for the client-side WebRTC engine (video-call.js).
/// Validates inputs and delegates to the JavaScript WebRTC API via IJSRuntime.
/// </summary>
internal sealed class WebRtcInteropService : IWebRtcInteropService
{
    private const string JsNamespace = "dotnetcloudVideoCall";
    private const int MaxSdpLengthBytes = 65_536;     // 64 KB
    private const int MaxIceCandidateLengthBytes = 4_096; // 4 KB
    private const int MaxPeerIdLength = 100;
    private const int MaxElementIdLength = 200;

    private static readonly HashSet<string> ValidStreamTypes = ["local", "screen"];
    private static readonly HashSet<string> ValidIceTransportPolicies = ["all", "relay"];

    private readonly IJSRuntime _js;
    private readonly ILogger<WebRtcInteropService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="WebRtcInteropService"/>.
    /// </summary>
    public WebRtcInteropService(IJSRuntime js, ILogger<WebRtcInteropService> logger)
    {
        _js = js ?? throw new ArgumentNullException(nameof(js));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> InitializeCallAsync<T>(DotNetObjectReference<T> dotNetRef, WebRtcCallConfig config) where T : class
    {
        ArgumentNullException.ThrowIfNull(dotNetRef);
        ArgumentNullException.ThrowIfNull(config);

        if (string.IsNullOrWhiteSpace(config.CallId))
        {
            throw new ArgumentException("CallId is required.", nameof(config));
        }

        if (config.IceServers is null || config.IceServers.Count == 0)
        {
            throw new ArgumentException("At least one ICE server is required.", nameof(config));
        }

        if (config.IceTransportPolicy is not null && !ValidIceTransportPolicies.Contains(config.IceTransportPolicy))
        {
            throw new ArgumentException($"Invalid IceTransportPolicy: '{config.IceTransportPolicy}'. Must be 'all' or 'relay'.", nameof(config));
        }

        foreach (var server in config.IceServers)
        {
            if (server.Urls is null || server.Urls.Length == 0)
            {
                throw new ArgumentException("Each ICE server must have at least one URL.", nameof(config));
            }
        }

        _logger.LogInformation("Initializing WebRTC call {CallId} with {IceServerCount} ICE servers", config.CallId, config.IceServers.Count);

        return await _js.InvokeAsync<bool>($"{JsNamespace}.initializeCall", dotNetRef, config);
    }

    /// <inheritdoc />
    public async Task<string?> StartLocalMediaAsync()
    {
        _logger.LogDebug("Starting local media capture");
        return await _js.InvokeAsync<string?>($"{JsNamespace}.startLocalMedia", (object?)null);
    }

    /// <inheritdoc />
    public async Task<string?> StartScreenShareAsync()
    {
        _logger.LogDebug("Starting screen share");
        return await _js.InvokeAsync<string?>($"{JsNamespace}.startScreenShare");
    }

    /// <inheritdoc />
    public async Task StopScreenShareAsync()
    {
        _logger.LogDebug("Stopping screen share");
        await _js.InvokeVoidAsync($"{JsNamespace}.stopScreenShare");
    }

    /// <inheritdoc />
    public async Task<string?> CreateOfferAsync(string peerId)
    {
        ValidatePeerId(peerId);
        _logger.LogDebug("Creating SDP offer for peer {PeerId}", peerId);
        return await _js.InvokeAsync<string?>($"{JsNamespace}.createOffer", peerId);
    }

    /// <inheritdoc />
    public async Task<string?> HandleOfferAsync(string peerId, string sdpJson)
    {
        ValidatePeerId(peerId);
        ValidateSdpPayload(sdpJson);
        _logger.LogDebug("Handling SDP offer from peer {PeerId}", peerId);
        return await _js.InvokeAsync<string?>($"{JsNamespace}.handleOffer", peerId, sdpJson);
    }

    /// <inheritdoc />
    public async Task<bool> HandleAnswerAsync(string peerId, string sdpJson)
    {
        ValidatePeerId(peerId);
        ValidateSdpPayload(sdpJson);
        _logger.LogDebug("Handling SDP answer from peer {PeerId}", peerId);
        return await _js.InvokeAsync<bool>($"{JsNamespace}.handleAnswer", peerId, sdpJson);
    }

    /// <inheritdoc />
    public async Task<bool> AddIceCandidateAsync(string peerId, string candidateJson)
    {
        ValidatePeerId(peerId);
        ValidateIceCandidate(candidateJson);
        return await _js.InvokeAsync<bool>($"{JsNamespace}.addIceCandidate", peerId, candidateJson);
    }

    /// <inheritdoc />
    public async Task ToggleAudioAsync(bool enabled)
    {
        _logger.LogDebug("Toggling audio: {Enabled}", enabled);
        await _js.InvokeVoidAsync($"{JsNamespace}.toggleAudio", enabled);
    }

    /// <inheritdoc />
    public async Task ToggleVideoAsync(bool enabled)
    {
        _logger.LogDebug("Toggling video: {Enabled}", enabled);
        await _js.InvokeVoidAsync($"{JsNamespace}.toggleVideo", enabled);
    }

    /// <inheritdoc />
    public async Task<bool> AttachStreamToElementAsync(string elementId, string streamType)
    {
        ValidateElementId(elementId);
        ValidateStreamType(streamType);
        return await _js.InvokeAsync<bool>($"{JsNamespace}.attachStreamToElement", elementId, streamType);
    }

    /// <inheritdoc />
    public async Task DetachStreamFromElementAsync(string elementId)
    {
        ValidateElementId(elementId);
        await _js.InvokeVoidAsync($"{JsNamespace}.detachStreamFromElement", elementId);
    }

    /// <inheritdoc />
    public async Task ClosePeerConnectionAsync(string peerId)
    {
        ValidatePeerId(peerId);
        _logger.LogDebug("Closing peer connection: {PeerId}", peerId);
        await _js.InvokeVoidAsync($"{JsNamespace}.closePeerConnection", peerId);
    }

    /// <inheritdoc />
    public async Task HangupAsync()
    {
        _logger.LogInformation("Hanging up WebRTC call");
        await _js.InvokeVoidAsync($"{JsNamespace}.hangup");
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        _logger.LogDebug("Disposing WebRTC interop");
        await _js.InvokeVoidAsync($"{JsNamespace}.dispose");
    }

    /// <inheritdoc />
    public async Task<WebRtcCallState?> GetCallStateAsync()
    {
        return await _js.InvokeAsync<WebRtcCallState?>($"{JsNamespace}.getCallState");
    }

    /// <inheritdoc />
    public async Task<WebRtcPeerState?> GetPeerStateAsync(string peerId)
    {
        ValidatePeerId(peerId);
        return await _js.InvokeAsync<WebRtcPeerState?>($"{JsNamespace}.getPeerState", peerId);
    }

    /// <inheritdoc />
    public async Task<WebRtcMediaState?> GetMediaStateAsync()
    {
        return await _js.InvokeAsync<WebRtcMediaState?>($"{JsNamespace}.getMediaState");
    }

    // ── Validation ────────────────────────────────────────────

    internal static void ValidatePeerId(string peerId)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            throw new ArgumentException("Peer ID is required.", nameof(peerId));
        }

        if (peerId.Length > MaxPeerIdLength)
        {
            throw new ArgumentException($"Peer ID exceeds maximum length of {MaxPeerIdLength}.", nameof(peerId));
        }
    }

    internal static void ValidateSdpPayload(string sdp)
    {
        if (string.IsNullOrWhiteSpace(sdp))
        {
            throw new ArgumentException("SDP payload is required.", nameof(sdp));
        }

        if (System.Text.Encoding.UTF8.GetByteCount(sdp) > MaxSdpLengthBytes)
        {
            throw new ArgumentException($"SDP payload exceeds maximum size of {MaxSdpLengthBytes} bytes.", nameof(sdp));
        }
    }

    internal static void ValidateIceCandidate(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            throw new ArgumentException("ICE candidate is required.", nameof(candidate));
        }

        if (System.Text.Encoding.UTF8.GetByteCount(candidate) > MaxIceCandidateLengthBytes)
        {
            throw new ArgumentException($"ICE candidate exceeds maximum size of {MaxIceCandidateLengthBytes} bytes.", nameof(candidate));
        }
    }

    internal static void ValidateElementId(string elementId)
    {
        if (string.IsNullOrWhiteSpace(elementId))
        {
            throw new ArgumentException("Element ID is required.", nameof(elementId));
        }

        if (elementId.Length > MaxElementIdLength)
        {
            throw new ArgumentException($"Element ID exceeds maximum length of {MaxElementIdLength}.", nameof(elementId));
        }
    }

    internal static void ValidateStreamType(string streamType)
    {
        if (string.IsNullOrWhiteSpace(streamType))
        {
            throw new ArgumentException("Stream type is required.", nameof(streamType));
        }

        // Valid types: "local", "screen", or a peer user ID (GUID-like string)
        if (!ValidStreamTypes.Contains(streamType) && !Guid.TryParse(streamType, out _))
        {
            throw new ArgumentException($"Invalid stream type: '{streamType}'. Must be 'local', 'screen', or a valid peer user ID.", nameof(streamType));
        }
    }
}
