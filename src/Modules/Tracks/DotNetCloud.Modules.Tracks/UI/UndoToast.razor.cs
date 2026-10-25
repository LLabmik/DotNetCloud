using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services.ModuleApis;
using DotNetCloud.UI.Shared.Components.DataDisplay;
using DotNetCloud.UI.Shared.Components.Dialogs;
using DotNetCloud.UI.Shared.Components.Forms;
using DotNetCloud.UI.Shared.Components.Navigation;
using DotNetCloud.UI.Shared.Components.Notifications;
using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>Code-behind for the <see cref="UndoToast"/> component.</summary>
public partial class UndoToast : ComponentBase, IAsyncDisposable
{
    /// <summary>The message to show (e.g., "Product deleted.").</summary>
    [Parameter] public string Message { get; set; } = "";

    /// <summary>How long the toast stays visible (in milliseconds). Default: 10 seconds.</summary>
    [Parameter] public int DurationMs { get; set; } = 10_000;

    /// <summary>Called when the user clicks "Undo". If null, no Undo button is shown.</summary>
    [Parameter] public Func<Task>? OnUndo { get; set; }

    /// <summary>Called when the toast is dismissed (timeout or manual close).</summary>
    [Parameter] public EventCallback OnDismissed { get; set; }

    private bool _isVisible;
    private bool _isLeaving;
    private bool _isUndoing;
    private CancellationTokenSource? _timerCts;

    private bool HasUndoAction => OnUndo is not null;

    /// <summary>Shows the toast. Call this after performing the action.</summary>
    public async Task ShowAsync()
    {
        _isVisible = true;
        _isLeaving = false;
        _isUndoing = false;
        StateHasChanged();

        // Auto-dismiss after timeout
        _timerCts?.Cancel();
        _timerCts = new CancellationTokenSource();
        try
        {
            await Task.Delay(DurationMs, _timerCts.Token);
            await DismissInternalAsync();
        }
        catch (OperationCanceledException)
        {
            // Timer was cancelled (user clicked something)
        }
    }

    private async Task HandleUndo()
    {
        if (OnUndo is null || _isUndoing) return;

        _isUndoing = true;
        _timerCts?.Cancel(); // Stop auto-dismiss

        try
        {
            await OnUndo();
        }
        finally
        {
            _isVisible = false;
            _isUndoing = false;
            await OnDismissed.InvokeAsync();
        }
    }

    private async Task Dismiss()
    {
        _timerCts?.Cancel();
        await DismissInternalAsync();
    }

    private async Task DismissInternalAsync()
    {
        if (!_isVisible) return;
        _isLeaving = true;
        StateHasChanged();
        await Task.Delay(200); // Match animation duration
        _isVisible = false;
        _isLeaving = false;
        await OnDismissed.InvokeAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _timerCts?.Cancel();
        _timerCts?.Dispose();
        await Task.CompletedTask;
    }
}
