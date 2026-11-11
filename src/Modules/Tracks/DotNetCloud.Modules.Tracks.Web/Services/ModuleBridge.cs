using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Tracks.Web.Services;

/// <summary>
/// The module UI's side of the frame contract with the core shell.
/// </summary>
/// <remarks>
/// <para>
/// The frame is sandboxed without <c>allow-same-origin</c>, so it has an opaque origin: no session
/// cookie is sent, and browser storage is unavailable. Everything that needs the core — navigation,
/// the shell's toasts, consent prompts, subresource URLs and the current theme — therefore goes through
/// the SDK bridge instead.
/// </para>
/// <para>
/// Requests are not routed through here: <c>js/dncModuleBridge.js</c> patches <c>window.fetch</c> when it
/// starts, which covers <see cref="System.Net.Http.HttpClient"/> because Blazor WebAssembly issues its
/// requests through fetch. Browser-built subresources (an <c>&lt;img src&gt;</c>) bypass that patch, so
/// their URLs must be produced by <see cref="MediaUrlAsync"/>.
/// </para>
/// </remarks>
public sealed class ModuleBridge : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private DotNetObjectReference<ModuleBridge>? _self;

    /// <summary>Initializes a new instance of the <see cref="ModuleBridge"/> class.</summary>
    /// <param name="js">JavaScript interop.</param>
    public ModuleBridge(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>Gets the theme the core shell last reported (<c>light</c> or <c>dark</c>).</summary>
    public string Theme { get; private set; } = "light";

    /// <summary>Gets the UI locale the core shell last reported.</summary>
    public string Locale { get; private set; } = string.Empty;

    /// <summary>Raised when the shell's theme or locale changes.</summary>
    public event Action? Changed;

    /// <summary>Subscribes to the shell's theme changes. Safe to call more than once.</summary>
    public async ValueTask InitializeAsync()
    {
        if (_self is not null)
        {
            return;
        }

        _self = DotNetObjectReference.Create(this);
        await _js.InvokeVoidAsync("tracksBridge.onTheme", _self);
    }

    /// <summary>Called by the bridge when the shell toggles its theme.</summary>
    /// <param name="theme">The new theme name.</param>
    /// <param name="locale">The new locale.</param>
    [JSInvokable]
    public void OnThemeChanged(string theme, string locale)
    {
        Theme = string.IsNullOrWhiteSpace(theme) ? "light" : theme;
        Locale = locale ?? string.Empty;
        Changed?.Invoke();
    }

    /// <summary>
    /// Appends the frame ticket to a URL the browser will request itself (images, downloads).
    /// </summary>
    /// <param name="url">A module-relative or core-relative URL.</param>
    /// <returns>The URL with the frame ticket attached.</returns>
    public ValueTask<string> MediaUrlAsync(string url) =>
        _js.InvokeAsync<string>("tracksBridge.mediaUrl", url);

    /// <summary>Asks the shell to navigate within this module.</summary>
    /// <param name="path">Module-relative path, e.g. <c>/products/{id}</c>.</param>
    public ValueTask NavigateAsync(string path) =>
        _js.InvokeVoidAsync("tracksBridge.navigate", path);

    /// <summary>Updates the document title shown by the shell.</summary>
    /// <param name="title">The title.</param>
    public ValueTask SetTitleAsync(string title) =>
        _js.InvokeVoidAsync("tracksBridge.setTitle", title);

    /// <summary>Shows a short toast in the core shell.</summary>
    /// <param name="message">The message.</param>
    public ValueTask NotifyAsync(string message) =>
        _js.InvokeVoidAsync("tracksBridge.notify", message);

    /// <summary>Asks the shell to render its consent prompt for the given scopes.</summary>
    /// <param name="scopes">Consent scopes, e.g. <c>files:read</c>.</param>
    public ValueTask RequestConsentAsync(params string[] scopes) =>
        _js.InvokeVoidAsync("tracksBridge.requestConsent", scopes);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _self?.Dispose();
        _self = null;
        await ValueTask.CompletedTask;
    }
}
