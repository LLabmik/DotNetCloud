using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Sdk.Web;

/// <summary>
/// The module UI's side of the frame contract with the core shell.
/// </summary>
/// <remarks>
/// <para>
/// A module frame is sandboxed without <c>allow-same-origin</c>, so it has an opaque origin: no
/// session cookie is sent and browser storage is unavailable. Anything that needs the core —
/// navigation, shell toasts, consent prompts, subresource URLs, opening an external link and the
/// current theme — travels over the SDK bridge (<c>js/dncModuleBridge.js</c>), wrapped for C# by
/// <c>js/dncModuleFrame.js</c>.
/// </para>
/// <para>
/// Requests are not routed through here: the bridge patches <c>window.fetch</c> when it starts,
/// which covers <see cref="System.Net.Http.HttpClient"/> because Blazor WebAssembly issues its
/// requests through fetch. Browser-built subresources (an <c>&lt;img src&gt;</c>) bypass that patch,
/// so their URLs must come from <see cref="MediaUrlAsync"/>.
/// </para>
/// </remarks>
public sealed class ModuleFrameBridge : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private DotNetObjectReference<ModuleFrameBridge>? _self;

    /// <summary>Initializes a new instance of the <see cref="ModuleFrameBridge"/> class.</summary>
    /// <param name="js">JavaScript interop.</param>
    public ModuleFrameBridge(IJSRuntime js)
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
    /// <returns>A task that completes once the subscription is installed.</returns>
    public async ValueTask InitializeAsync()
    {
        if (_self is not null)
        {
            return;
        }

        _self = DotNetObjectReference.Create(this);
        await _js.InvokeVoidAsync("dncModuleFrame.onTheme", _self);
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
        _js.InvokeAsync<string>("dncModuleFrame.mediaUrl", url);

    /// <summary>Asks the shell to navigate within this module.</summary>
    /// <param name="path">Module-relative path.</param>
    /// <returns>A task that completes when the message is posted.</returns>
    public ValueTask NavigateAsync(string path) =>
        _js.InvokeVoidAsync("dncModuleFrame.navigate", path);

    /// <summary>Updates the document title shown by the shell.</summary>
    /// <param name="title">The title.</param>
    /// <returns>A task that completes when the message is posted.</returns>
    public ValueTask SetTitleAsync(string title) =>
        _js.InvokeVoidAsync("dncModuleFrame.setTitle", title);

    /// <summary>Shows a short toast in the core shell.</summary>
    /// <param name="message">The message.</param>
    /// <returns>A task that completes when the message is posted.</returns>
    public ValueTask NotifyAsync(string message) =>
        _js.InvokeVoidAsync("dncModuleFrame.notify", message);

    /// <summary>Asks the shell to open a link outside the frame.</summary>
    /// <param name="url">An absolute http(s) URL.</param>
    /// <returns>A task that completes when the message is posted.</returns>
    public ValueTask OpenExternalAsync(string url) =>
        _js.InvokeVoidAsync("dncModuleFrame.openExternal", url);

    /// <summary>Asks the shell to render its consent prompt for the given scopes.</summary>
    /// <param name="scopes">Consent scopes, e.g. <c>files:read</c>.</param>
    /// <returns>A task that completes when the message is posted.</returns>
    /// <remarks>
    /// The array is cast to <see cref="object"/> on purpose: without the cast Blazor treats a
    /// <c>string[]</c> as the *argument list*, so a single scope arrives in JavaScript as a bare
    /// string. The SDK bridge only forwards an array (<c>Array.isArray</c>), so the request would be
    /// silently dropped and the shell would never show its permission band.
    /// </remarks>
    public ValueTask RequestConsentAsync(params string[] scopes) =>
        _js.InvokeVoidAsync("dncModuleFrame.requestConsent", (object)scopes);

    /// <summary>Copies plain text to the clipboard.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when the browser accepted the copy.</returns>
    public ValueTask<bool> CopyTextAsync(string text) =>
        _js.InvokeAsync<bool>("dncModuleFrame.copyText", text);

    /// <summary>Copies a rendered element's content as rich text.</summary>
    /// <param name="elementId">The element id.</param>
    /// <returns><see langword="true"/> when the browser accepted the copy.</returns>
    public ValueTask<bool> CopyRenderedAsync(string elementId) =>
        _js.InvokeAsync<bool>("dncModuleFrame.copyRendered", elementId);

    /// <summary>Scrolls an element to the end of its content.</summary>
    /// <param name="elementId">The element id.</param>
    /// <returns>A task that completes once the scroll was requested.</returns>
    public ValueTask ScrollToBottomAsync(string elementId) =>
        _js.InvokeVoidAsync("dncModuleFrame.scrollToBottom", elementId);

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _self?.Dispose();
        _self = null;
        return ValueTask.CompletedTask;
    }
}
