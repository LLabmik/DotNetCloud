using DotNetCloud.Core.Modules.Ui;
using DotNetCloud.UI.Web.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DotNetCloud.UI.Web.Components.Modules;

/// <summary>
/// Renders a module's Home-page widget as a sandboxed iframe served by the module itself.
/// </summary>
/// <remarks>
/// First pass of the widget plane: the card body is the module's own document rather than a
/// compiled Blazor component. It reuses the app-host bridge, so the same source-bound message rules
/// apply — a widget frame cannot spoof a height, title or notification for another widget, because
/// each mount only listens to its own <c>contentWindow</c>.
/// </remarks>
public partial class ModuleWidgetFrame : ComponentBase, IAsyncDisposable
{
    /// <summary>Default card height before the frame reports its own.</summary>
    private const int DefaultHeight = 220;

    private ElementReference _frame;
    private DotNetObjectReference<ModuleWidgetFrame>? _selfRef;
    private IJSObjectReference? _handle;
    private int _height = DefaultHeight;
    private bool _failed;

    /// <summary>Gets or sets the owning module id.</summary>
    [Parameter, EditorRequired]
    public string ModuleId { get; set; } = string.Empty;

    /// <summary>Gets or sets the card title.</summary>
    [Parameter]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the module-relative path the widget frame loads.</summary>
    [Parameter]
    public string Path { get; set; } = "/";

    /// <summary>Gets the site-relative proxy URL the frame loads.</summary>
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

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private ToastService Toast { get; set; } = default!;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || Source.Length == 0)
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
                title = Title,
                role = "widget",
                handshakeTimeoutMs = 8000,
            });
    }

    /// <summary>Raised when the widget frame completed the handshake.</summary>
    /// <returns>A completed task.</returns>
    [JSInvokable]
    public Task OnReady()
    {
        _failed = false;
        return InvokeAsync(StateHasChanged);
    }

    /// <summary>Raised when the frame reports its document height.</summary>
    /// <param name="height">The reported height in CSS pixels.</param>
    /// <returns>A task that completes when the height has been applied.</returns>
    [JSInvokable]
    public Task OnResize(int height)
    {
        var clamped = ModuleAppProtocol.ClampHeight(height);
        if (clamped == _height)
        {
            return Task.CompletedTask;
        }

        _height = clamped;
        return InvokeAsync(StateHasChanged);
    }

    /// <summary>Raised when the frame never reported readiness.</summary>
    /// <returns>A task that completes when the fallback has been rendered.</returns>
    [JSInvokable]
    public Task OnHandshakeTimeout()
    {
        _failed = true;
        return InvokeAsync(StateHasChanged);
    }

    /// <summary>Raised when the frame failed to load.</summary>
    /// <returns>A task that completes when the fallback has been rendered.</returns>
    [JSInvokable]
    public Task OnLoadError()
    {
        _failed = true;
        return InvokeAsync(StateHasChanged);
    }

    /// <summary>Raised when the frame asks for a title change; a card title is fixed by the shell.</summary>
    /// <param name="title">The requested title.</param>
    /// <returns>A completed task.</returns>
    [JSInvokable]
    public Task OnTitle(string title) => Task.CompletedTask;

    /// <summary>Raised when the frame asks to navigate.</summary>
    /// <param name="path">The requested module-relative path.</param>
    /// <returns>A task that completes when navigation has been issued.</returns>
    [JSInvokable]
    public Task OnNavigate(string path)
    {
        var shellPath = ModuleAppUrls.ShellPath(ModuleId);
        if (shellPath.Length == 0)
        {
            return Task.CompletedTask;
        }

        var safePath = ModuleAppUrls.NormalizeEntryPath(path);
        Navigation.NavigateTo(safePath == "/" ? shellPath : shellPath + safePath);
        return Task.CompletedTask;
    }

    /// <summary>Raised when the frame asks to open an external link.</summary>
    /// <param name="url">The requested URL.</param>
    /// <returns>A task that completes when the request has been handled.</returns>
    [JSInvokable]
    public async Task OnOpenExternal(string url)
    {
        if (!ModuleAppUrls.IsAllowedExternalUrl(url) || _handle is null)
        {
            return;
        }

        await _handle.InvokeVoidAsync("openExternal", url);
    }

    /// <summary>Raised when the frame asks for consent; a widget has no prompt surface.</summary>
    /// <param name="scopes">The requested scopes.</param>
    /// <returns>A completed task.</returns>
    [JSInvokable]
    public Task OnRequestConsent(string[]? scopes) => Task.CompletedTask;

    /// <summary>Raised when the frame wants to show a toast.</summary>
    /// <param name="message">The message body.</param>
    /// <returns>A completed task.</returns>
    [JSInvokable]
    public Task OnNotify(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return Task.CompletedTask;
        }

        Toast.ShowInfo($"{Title}: {message}");
        return Task.CompletedTask;
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
