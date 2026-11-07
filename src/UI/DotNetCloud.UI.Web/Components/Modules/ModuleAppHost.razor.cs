using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Ui;
using DotNetCloud.UI.Web.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DotNetCloud.UI.Web.Components.Modules;

/// <summary>
/// Hosts a module's own web UI in a sandboxed iframe and brokers the <c>postMessage</c> protocol.
/// </summary>
/// <remarks>
/// <para>
/// The sandbox is deliberately <c>allow-scripts allow-forms allow-popups</c> with <b>no</b>
/// <c>allow-same-origin</c>. The frame therefore has an opaque origin: it cannot read core storage,
/// cookies or the DOM, and every message it sends reports <c>event.origin === "null"</c>. The peer
/// is authenticated by identity instead — the JS bridge only forwards messages whose
/// <c>event.source</c> is this exact frame — plus a direction-scoped type allow-list enforced in
/// both the bridge and this component.
/// </para>
/// <para>
/// Module UIs keep their state server-side behind the proxied <c>/apps/{moduleId}/**</c> path; the
/// frame holds no credentials. Authentication reaches the module backend as the core-signed
/// <c>dnc-user-assertion</c> that the proxy injects after stripping the browser's cookies.
/// </para>
/// </remarks>
public partial class ModuleAppHost : ComponentBase, IAsyncDisposable
{
    /// <summary>Default time to wait for the frame's <c>dnc:ready</c> before showing a failure.</summary>
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);

    private ElementReference _frame;
    private DotNetObjectReference<ModuleAppHost>? _selfRef;
    private IJSObjectReference? _handle;
    private ModuleAppHostState _state = ModuleAppHostState.Starting;
    private string _title = string.Empty;
    private string _failureMessage = string.Empty;
    private IReadOnlyList<string> _requestedScopes = [];

    /// <summary>Gets or sets the module id whose UI is framed.</summary>
    [Parameter, EditorRequired]
    public string ModuleId { get; set; } = string.Empty;

    /// <summary>Gets or sets the display title of the module app.</summary>
    [Parameter]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the module-relative path to load (for example <c>/</c> or <c>/settings</c>).
    /// </summary>
    [Parameter]
    public string Path { get; set; } = "/";

    /// <summary>Gets the site-relative proxy URL the iframe loads.</summary>
    private string Source
    {
        get
        {
            var basePath = ModuleAppUrls.BasePath(ModuleId);
            if (basePath.Length == 0)
            {
                return string.Empty;
            }

            var path = ModuleAppUrls.NormalizeEntryPath(Path);
            return path == "/" ? basePath + "/" : basePath + path;
        }
    }

    /// <summary>Gets a CSS token describing the current host state.</summary>
    private string StateToken => _state switch
    {
        ModuleAppHostState.Starting => "starting",
        ModuleAppHostState.Running => "running",
        ModuleAppHostState.Stopped => "stopped",
        _ => "failed",
    };

    /// <summary>Gets a value indicating whether the iframe should be in the DOM.</summary>
    private bool ShowFrame => _state is ModuleAppHostState.Starting or ModuleAppHostState.Running;

    /// <summary>Gets the link to the user's own consent manager.</summary>
    private static string ConnectedAppsHref => "/connected-apps";

    /// <summary>Gets the display title, preferring a title pushed by the frame.</summary>
    private string DisplayTitle => string.IsNullOrWhiteSpace(_title) ? Title : _title;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private ToastService Toast { get; set; } = default!;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (string.IsNullOrWhiteSpace(_title))
        {
            _title = Title;
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !ShowFrame || Source.Length == 0)
        {
            return;
        }

        _selfRef ??= DotNetObjectReference.Create(this);
        _handle ??= await JSRuntime.InvokeAsync<IJSObjectReference>(
            "dotnetcloudModuleApp.mount",
            _frame,
            _selfRef,
            new
            {
                moduleId = ModuleId,
                title = DisplayTitle,
                role = "app",
                handshakeTimeoutMs = (int)HandshakeTimeout.TotalMilliseconds,
            });
    }

    /// <summary>Raised by the bridge when the frame has mounted the module UI.</summary>
    /// <returns>A task that completes when the state has been applied.</returns>
    [JSInvokable]
    public Task OnReady()
    {
        _state = ModuleAppHostState.Running;
        _failureMessage = string.Empty;
        return InvokeAsync(StateHasChanged);
    }

    /// <summary>Raised by the bridge when the frame never reported readiness.</summary>
    /// <returns>A task that completes when the state has been applied.</returns>
    [JSInvokable]
    public Task OnHandshakeTimeout()
    {
        if (_state == ModuleAppHostState.Starting)
        {
            _state = ModuleAppHostState.Stopped;
            _failureMessage = "The app did not respond. It may be stopped or restarting.";
        }

        return InvokeAsync(StateHasChanged);
    }

    /// <summary>Raised by the bridge when the frame failed to load.</summary>
    /// <returns>A task that completes when the state has been applied.</returns>
    [JSInvokable]
    public Task OnLoadError()
    {
        _state = ModuleAppHostState.Failed;
        _failureMessage = "The app could not be loaded.";
        return InvokeAsync(StateHasChanged);
    }

    /// <summary>Raised by the frame when it reports a new document height.</summary>
    /// <param name="height">The reported height in CSS pixels (already clamped by the bridge).</param>
    /// <returns>A completed task.</returns>
    /// <remarks>
    /// The full-page host fills its container, so the height contract only matters to the widget
    /// frame (<c>ModuleWidgetFrame</c>), which applies it to its own iframe.
    /// </remarks>
    [JSInvokable]
    public Task OnResize(int height)
    {
        ModuleAppProtocol.ClampHeight(height);
        return Task.CompletedTask;
    }

    /// <summary>Raised by the frame when it wants the window title to change.</summary>
    /// <param name="title">The requested title.</param>
    /// <returns>A task that completes when the title has been applied.</returns>
    [JSInvokable]
    public Task OnTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > ModuleAppProtocol.MaxTitleLength)
        {
            return Task.CompletedTask;
        }

        _title = title.Trim();
        return InvokeAsync(StateHasChanged);
    }

    /// <summary>Raised by the frame when it wants to navigate within itself.</summary>
    /// <param name="path">A module-relative path.</param>
    /// <returns>A task that completes when navigation has been issued.</returns>
    [JSInvokable]
    public async Task OnNavigate(string path)
    {
        var safePath = ModuleAppUrls.NormalizeEntryPath(path);
        if (safePath == "/" && !string.IsNullOrWhiteSpace(path) && path.Trim() != "/")
        {
            // NormalizeEntryPath collapses anything suspicious to "/"; do not navigate on junk.
            return;
        }

        if (_handle is not null)
        {
            await _handle.InvokeVoidAsync("navigate", Source.TrimEnd('/') + (safePath == "/" ? "/" : safePath));
        }
    }

    /// <summary>Raised by the frame when it asks to open an external link.</summary>
    /// <param name="url">The requested URL.</param>
    /// <returns>A task that completes when the request has been handled.</returns>
    [JSInvokable]
    public async Task OnOpenExternal(string url)
    {
        if (!ModuleAppUrls.IsAllowedExternalUrl(url))
        {
            return;
        }

        if (_handle is not null)
        {
            await _handle.InvokeVoidAsync("openExternal", url);
        }
    }

    /// <summary>Raised by the frame when it asks the core to prompt for consent.</summary>
    /// <param name="scopes">The scopes the module is asking for.</param>
    /// <returns>A task that completes when the prompt has been rendered.</returns>
    [JSInvokable]
    public Task OnRequestConsent(string[]? scopes)
    {
        _requestedScopes = (scopes ?? [])
            .Where(ModuleScopes.IsConsentScope)
            .Distinct(StringComparer.Ordinal)
            .Take(32)
            .ToList();

        if (_requestedScopes.Count == 0)
        {
            return Task.CompletedTask;
        }

        return InvokeAsync(StateHasChanged);
    }

    /// <summary>Raised by the frame when it wants to show a toast.</summary>
    /// <param name="message">The message body.</param>
    /// <returns>A task that completes when the toast has been shown.</returns>
    [JSInvokable]
    public Task OnNotify(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return Task.CompletedTask;
        }

        var text = message.Length > ModuleAppProtocol.MaxNotificationLength
            ? message[..ModuleAppProtocol.MaxNotificationLength]
            : message;

        Toast.ShowInfo($"{DisplayTitle}: {text}");
        return Task.CompletedTask;
    }

    /// <summary>Dismisses the consent banner.</summary>
    private Task DismissConsent()
    {
        _requestedScopes = [];
        return InvokeAsync(StateHasChanged);
    }

    /// <summary>Describes a scope in plain language.</summary>
    /// <param name="scope">The scope name.</param>
    /// <returns>Text suitable for the banner.</returns>
    private static string DescribeScope(string scope) => ModuleScopes.Describe(scope);

    /// <summary>Reloads the frame after a failure.</summary>
    private Task RetryAsync()
    {
        _state = ModuleAppHostState.Starting;
        _failureMessage = string.Empty;
        return InvokeAsync(StateHasChanged);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_handle is not null)
            {
                await _handle.InvokeVoidAsync("dispose");
                await _handle.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone; there is nothing left to clean up on the browser side.
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            _selfRef?.Dispose();
        }
    }
}

/// <summary>
/// Lifecycle state of a framed module app.
/// </summary>
public enum ModuleAppHostState
{
    /// <summary>The frame is loading and has not reported readiness yet.</summary>
    Starting,

    /// <summary>The module UI mounted and completed the handshake.</summary>
    Running,

    /// <summary>The module process is not running, or never answered the handshake.</summary>
    Stopped,

    /// <summary>The frame failed to load.</summary>
    Failed,
}
