using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Ui;
using DotNetCloud.UI.Web.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DotNetCloud.UI.Web.Components.Modules;

/// <summary>
/// Hosts a module's own web UI in a sandboxed iframe and brokers the <c>postMessage</c> protocol.
/// </summary>
/// <remarks>
/// <para>
/// The sandbox is deliberately <c>allow-scripts allow-forms allow-popups allow-modals</c> with
/// <b>no</b> <c>allow-same-origin</c>. The frame therefore has an opaque origin: it cannot read core
/// storage, cookies or the DOM, and every message it sends reports
/// <c>event.origin === "null"</c>. The peer is authenticated by identity instead — the JS bridge only
/// forwards messages whose <c>event.source</c> is this exact frame — plus a direction-scoped type
/// allow-list enforced in both the bridge and this component.
/// </para>
/// <para>
/// <c>allow-modals</c> is granted because a module UI is expected to use dialogs for confirmation
/// and other blocking questions. It permits <c>alert</c>/<c>confirm</c>/<c>prompt</c> and
/// <c>&lt;dialog&gt;.showModal()</c>; it does <b>not</b> grant any access to the core origin, the
/// clipboard, geolocation or storage.</para>
/// <para>
/// Module UIs keep their state server-side behind the proxied <c>/apps/{moduleId}/**</c> path; the
/// frame holds no credentials. Authentication reaches the module backend as the core-signed
/// <c>dnc-user-assertion</c> that the proxy injects after stripping the browser's cookies.
/// </para>
/// </remarks>
public partial class ModuleAppHost : ComponentBase, IAsyncDisposable
{
    /// <summary>Default time to wait for the frame's <c>dnc:ready</c> before showing a failure.</summary>
    // A framed module boots a full Blazor WebAssembly app. Ten seconds was enough only when the
    // browser had the bundle cached: after a deploy every _framework file has a new hash, the first
    // load is cold, and the shell declared a perfectly healthy module dead — "The app did not
    // respond. It may be stopped or restarting." — and unmounted it. The budget is measured from the
    // frame's load event (see module-app-host.js), so this only covers the app's own boot.
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(60);

    private ElementReference _frame;
    private DotNetObjectReference<ModuleAppHost>? _selfRef;
    private IJSObjectReference? _handle;
    private ModuleAppHostState _state = ModuleAppHostState.Starting;
    private string _title = string.Empty;
    private string _failureMessage = string.Empty;
    private IReadOnlyList<string> _requestedScopes = [];
    private bool _consentBusy;
    private string? _frameTicket;

    /// <summary>True while the mount bridge is being resolved, so a re-render cannot mount twice.</summary>
    private bool _mounting;

    /// <summary>The module the cached frame ticket was minted for, so a changed id re-mints.</summary>
    private string? _ticketModuleId;

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

    /// <summary>
    /// Gets or sets the query string the shell page was opened with, forwarded to the frame.
    /// </summary>
    /// <remarks>
    /// The shell keeps the browser URL (<c>/modules/{moduleId}?photoId=…</c>) while the frame loads
    /// <c>/apps/{moduleId}/**</c>. Without forwarding, a deep link would land on the module's start
    /// page and drop what the user clicked.
    /// </remarks>
    [Parameter]
    public string Query { get; set; } = string.Empty;

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
            var url = path == "/" ? basePath + "/" : basePath + path;

            var query = ModuleAppUrls.NormalizeFrameQuery(Query);
            if (query.Length > 0)
            {
                url += query;
            }

            return ModuleFrameSource.WithTicket(url, _frameTicket);
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
    // The iframe's src carries the ticket, so it must not render until the ticket exists: an
    // unticketed src loads once (401), then the real src loads again, and every extra navigation
    // aborts the one in flight (ERR_ABORTED) - fatal for a cold WebAssembly boot.
    private bool ShowFrame => _frameTicket is not null
        && _state is ModuleAppHostState.Starting or ModuleAppHostState.Running;

    /// <summary>Gets the link to the user's own consent manager.</summary>
    private static string ConnectedAppsHref => "/connected-apps";

    /// <summary>
    /// Gets the module name shown in the permission bar.
    /// </summary>
    /// <remarks>
    /// The bar is the first thing a user reads about a module, so it must never show the reverse-DNS
    /// id ("dotnetcloud.music is asking for permission"). A manifest-less module falls back to the id,
    /// which is why the id is humanised here instead of being trusted as a display name.
    /// </remarks>
    private string ConsentModuleName => ToDisplayName(Title, ModuleId);

    /// <summary>Humanises a module id or passes through a real display name.</summary>
    /// <param name="title">The descriptor title, if any.</param>
    /// <param name="moduleId">The module id used as the fallback.</param>
    /// <returns>A name suitable for the permission bar.</returns>
    internal static string ToDisplayName(string? title, string moduleId)
    {
        var candidate = string.IsNullOrWhiteSpace(title) ? moduleId : title;

        if (string.IsNullOrWhiteSpace(candidate) || !candidate.Contains('.', StringComparison.Ordinal))
        {
            return candidate;
        }

        // "dotnetcloud.music" -> "Music", "org.example.my-app" -> "My App".
        var lastSegment = candidate.Split('.')[^1];
        var words = lastSegment
            .Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]);

        return string.Join(' ', words);
    }

    /// <summary>Gets the display title, preferring a title pushed by the frame.</summary>
    private string DisplayTitle => string.IsNullOrWhiteSpace(_title) ? Title : _title;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private ToastService Toast { get; set; } = default!;

    [Inject]
    private DotNetCloudApiClient Api { get; set; } = default!;

    [Inject]
    private IModuleFrameTickets FrameTickets { get; set; } = default!;

    [Inject]
    private AuthenticationStateProvider AuthenticationState { get; set; } = default!;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (string.IsNullOrWhiteSpace(_title))
        {
            _title = Title;
        }
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        // The frame runs with an opaque origin, so it cannot send the session cookie. The ticket in
        // the URL is its credential; the SDK bridge forwards it on the module's own API calls.
        //
        // Mint it ONCE per module: the iframe's src is bound to this value, so issuing a new ticket on
        // every parameter set changed the attribute on every shell re-render (module health polls,
        // navigation-registry changes) and restarted the frame's navigation - each restart aborted the
        // in-flight load with ERR_ABORTED, so a cold WebAssembly boot never finished and the shell
        // reported "The app did not respond. It may be stopped or restarting."
        if (_frameTicket is not null && string.Equals(_ticketModuleId, ModuleId, StringComparison.Ordinal))
        {
            return;
        }

        _ticketModuleId = ModuleId;
        _frameTicket = await ModuleFrameSource.IssueAsync(AuthenticationState, FrameTickets, ModuleId);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Mount when the frame element first exists, which is after the ticket resolves - not on the
        // first render, when ShowFrame is still false.
        if (!ShowFrame || Source.Length == 0 || _handle is not null || _mounting)
        {
            return;
        }

        _mounting = true;

        try
        {
            _selfRef ??= DotNetObjectReference.Create(this);
            _handle = await JSRuntime.InvokeAsync<IJSObjectReference>(
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
        finally
        {
            _mounting = false;
        }
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

    /// <summary>
    /// Approves the scopes the frame asked for, then reloads the frame so the retried call goes
    /// through the consent gate.
    /// </summary>
    /// <remarks>
    /// The core validates the request against the manifest it loaded, so this only ever writes
    /// scopes the module declared. Without this action the banner offered the user nothing but a
    /// link to a read-only list — the prompt could never be approved.
    /// </remarks>
    /// <returns>A task that completes when the grant has been recorded.</returns>
    private async Task ApproveConsentAsync()
    {
        if (_requestedScopes.Count == 0 || _consentBusy)
        {
            return;
        }

        _consentBusy = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            var approved = await Api.GrantModuleConsentAsync(ModuleId, _requestedScopes);

            if (!approved)
            {
                Toast.ShowError($"Could not record the permission for {DisplayTitle}.");
                return;
            }

            _requestedScopes = [];
            Toast.ShowSuccess($"Permission granted to {DisplayTitle}.");

            if (_handle is not null)
            {
                // Reload the frame in place: the module retries its own call on load.
                await _handle.InvokeVoidAsync("navigate", Source);
            }
        }
        finally
        {
            _consentBusy = false;
            await InvokeAsync(StateHasChanged);
        }
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
