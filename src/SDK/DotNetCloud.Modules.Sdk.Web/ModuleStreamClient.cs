using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Sdk.Web;

/// <summary>
/// Drives a Server-Sent-Events response from a module host and raises its payloads as C# events.
/// </summary>
/// <remarks>
/// <para>
/// <c>HttpClient</c> cannot surface a streaming response body inside WebAssembly, so the pump lives in
/// <c>js/dncModuleFrame.js</c>: it POSTs the JSON body, reads the response stream and forwards each
/// <c>data:</c> payload one at a time. The request is still authenticated, because the SDK bridge
/// patches <c>window.fetch</c>.
/// </para>
/// <para>
/// The class is scoped: one stream per UI instance. <see cref="StartAsync"/> stops any stream that is
/// still running, so a double send cannot interleave two answers.
/// </para>
/// </remarks>
public sealed class ModuleStreamClient : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private DotNetObjectReference<ModuleStreamClient>? _self;

    /// <summary>Initializes a new instance of the <see cref="ModuleStreamClient"/> class.</summary>
    /// <param name="js">JavaScript interop.</param>
    public ModuleStreamClient(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>Raised for each <c>data:</c> payload the host sends.</summary>
    public event Action<string>? Received;

    /// <summary>Raised when the host answered with a non-success status, or the stream broke.</summary>
    public event Action<int>? Failed;

    /// <summary>Raised once when the stream ends normally.</summary>
    public event Action? Completed;

    /// <summary>Starts a streaming POST.</summary>
    /// <param name="url">A module-relative URL.</param>
    /// <param name="body">The JSON body.</param>
    /// <returns>A task that completes once the stream has been started.</returns>
    public async Task StartAsync(string url, object body)
    {
        _self ??= DotNetObjectReference.Create(this);
        await _js.InvokeVoidAsync("dncModuleFrame.stream", url, body, _self);
    }

    /// <summary>Aborts the running stream, if any.</summary>
    /// <returns>A task that completes once the abort was requested.</returns>
    public async Task StopAsync() => await _js.InvokeVoidAsync("dncModuleFrame.stopStream");

    /// <summary>Called by the pump for every server event.</summary>
    /// <param name="payload">The raw <c>data:</c> payload.</param>
    [JSInvokable]
    public void OnStreamEvent(string payload) => Received?.Invoke(payload);

    /// <summary>Called by the pump when the host refused the stream.</summary>
    /// <param name="status">The HTTP status, or 0 when the stream broke before answering.</param>
    [JSInvokable]
    public void OnStreamFailed(int status) => Failed?.Invoke(status);

    /// <summary>Called by the pump when the stream ended.</summary>
    [JSInvokable]
    public void OnStreamCompleted() => Completed?.Invoke();

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _self?.Dispose();
        _self = null;
        return ValueTask.CompletedTask;
    }
}
