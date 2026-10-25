using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Client.Shared;

/// <summary>Code-behind for the <see cref="ConfirmDialog"/> component.</summary>
public partial class ConfirmDialog : ComponentBase
{
    private bool _visible;
    private TaskCompletionSource<bool>? _tcs;

    [Parameter]
    public string Title { get; set; } = "Confirm";

    [Parameter]
    public string Message { get; set; } = "Are you sure?";

    [Parameter]
    public string ConfirmText { get; set; } = "Confirm";

    [Parameter]
    public string CancelText { get; set; } = "Cancel";

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
