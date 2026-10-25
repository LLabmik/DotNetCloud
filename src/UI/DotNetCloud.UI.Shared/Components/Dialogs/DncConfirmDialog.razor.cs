using DotNetCloud.Core.Localization;
using DotNetCloud.UI.Shared.Components.DataDisplay;
using DotNetCloud.UI.Shared.Components.Dialogs;
using DotNetCloud.UI.Shared.Components.Editors;
using DotNetCloud.UI.Shared.Components.Forms;
using DotNetCloud.UI.Shared.Components.Layout;
using DotNetCloud.UI.Shared.Components.Navigation;
using DotNetCloud.UI.Shared.Components.Notifications;
using DotNetCloud.UI.Shared.Resources;
using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace DotNetCloud.UI.Shared.Components.Dialogs;

/// <summary>Code-behind for the <see cref="DncConfirmDialog"/> component.</summary>
public partial class DncConfirmDialog : ComponentBase
{
    private bool _visible;
    private TaskCompletionSource<bool>? _tcs;

    /// <summary>Dialog title.</summary>
    [Parameter]
    public string Title { get; set; } = "Confirm";

    /// <summary>Message body.</summary>
    [Parameter]
    public string Message { get; set; } = "Are you sure?";

    /// <summary>Text for the confirm button.</summary>
    [Parameter]
    public string ConfirmText { get; set; } = "Confirm";

    /// <summary>Text for the cancel button.</summary>
    [Parameter]
    public string CancelText { get; set; } = "Cancel";

    /// <summary>CSS class for the confirm button (e.g. btn-danger, btn-primary).</summary>
    [Parameter]
    public string ConfirmButtonClass { get; set; } = "btn-danger";

    /// <summary>
    /// Shows the dialog and returns true if confirmed, false if cancelled.
    /// </summary>
    public Task<bool> ShowAsync(string? message = null, string? title = null)
    {
        if (message is not null) Message = message;
        if (title is not null) Title = title;
        _tcs = new TaskCompletionSource<bool>();
        _visible = true;
        StateHasChanged();
        return _tcs.Task;
    }

    private void OnConfirmClicked()
    {
        _visible = false;
        _tcs?.TrySetResult(true);
        StateHasChanged();
    }

    private void OnCancel()
    {
        _visible = false;
        _tcs?.TrySetResult(false);
        StateHasChanged();
    }
}
