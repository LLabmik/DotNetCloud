using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DotNetCloud.UI.Web.Components.Shared;

/// <summary>
/// Invisible global component (rendered from MainLayout, interactive server) that wires the
/// <c>dotnetcloudPresence</c> JS listener and forwards reported activity to
/// <see cref="IPresenceActivityReporter"/>, keeping the signed-in user's presence green while
/// they interact with any page. Left idle, the server flips them to Away/yellow automatically.
/// </summary>
public partial class PresenceActivityReporter : ComponentBase, IAsyncDisposable
{
    private DotNetObjectReference<PresenceActivityReporter>? _objectReference;
    private bool _attached;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    [Inject]
    private IPresenceActivityReporter Reporter { get; set; } = default!;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _attached)
        {
            return;
        }

        try
        {
            _objectReference = DotNetObjectReference.Create(this);
            await JsRuntime.InvokeVoidAsync("dotnetcloudPresence.attach", _objectReference);
            _attached = true;
        }
        catch (JSDisconnectedException)
        {
            // Circuit already disconnected — nothing to attach to; presence falls back to
            // connect-time seeding and the disconnect flow.
        }
        catch (InvalidOperationException)
        {
            // JS interop not available (e.g. prerender). Retried on the next render.
        }
    }

    /// <summary>
    /// JS-invokable activity report (throttled client-side by <c>presence-activity.js</c>).
    /// </summary>
    [JSInvokable]
    public Task ReportActivityAsync()
    {
        return Reporter.ReportActivityAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_objectReference is not null)
        {
            try
            {
                await JsRuntime.InvokeVoidAsync("dotnetcloudPresence.dispose", _objectReference);
            }
            catch (JSDisconnectedException)
            {
                // Ignored on circuit teardown.
            }
            catch (InvalidOperationException)
            {
                // JS interop not available.
            }

            _objectReference.Dispose();
            _objectReference = null;
        }
    }
}
