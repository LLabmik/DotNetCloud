using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Host-safe fallback used when browser JavaScript interop is unavailable.
/// </summary>
public sealed class NoOpWebRtcInteropService : IWebRtcInteropService
{
    public Task<bool> InitializeCallAsync<T>(DotNetObjectReference<T> dotNetRef, WebRtcCallConfig config) where T : class
        => Task.FromResult(false);

    public Task<string?> StartLocalMediaAsync() => Task.FromResult<string?>(null);

    public Task<string?> StartScreenShareAsync() => Task.FromResult<string?>(null);

    public Task StopScreenShareAsync() => Task.CompletedTask;

    public Task<string?> CreateOfferAsync(string peerId) => Task.FromResult<string?>(null);

    public Task<string?> HandleOfferAsync(string peerId, string sdpJson) => Task.FromResult<string?>(null);

    public Task<bool> HandleAnswerAsync(string peerId, string sdpJson) => Task.FromResult(false);

    public Task<bool> AddIceCandidateAsync(string peerId, string candidateJson) => Task.FromResult(false);

    public Task ToggleAudioAsync(bool enabled) => Task.CompletedTask;

    public Task ToggleVideoAsync(bool enabled) => Task.CompletedTask;

    public Task<bool> AttachStreamToElementAsync(string elementId, string streamType) => Task.FromResult(false);

    public Task DetachStreamFromElementAsync(string elementId) => Task.CompletedTask;

    public Task ClosePeerConnectionAsync(string peerId) => Task.CompletedTask;

    public Task HangupAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    public Task<WebRtcCallState?> GetCallStateAsync() => Task.FromResult<WebRtcCallState?>(null);

    public Task<WebRtcPeerState?> GetPeerStateAsync(string peerId) => Task.FromResult<WebRtcPeerState?>(null);

    public Task<WebRtcMediaState?> GetMediaStateAsync() => Task.FromResult<WebRtcMediaState?>(null);

    public Task<bool> SetBackgroundBlurAsync(bool enabled) => Task.FromResult(false);

    public Task<bool> IsBackgroundBlurSupportedAsync() => Task.FromResult(false);

    public Task<bool> SetVirtualBackgroundAsync(string imageUrl) => Task.FromResult(false);

    public Task SetBlurIntensityAsync(int intensity) => Task.CompletedTask;

    public Task<int> GetBlurIntensityAsync() => Task.FromResult(0);
}