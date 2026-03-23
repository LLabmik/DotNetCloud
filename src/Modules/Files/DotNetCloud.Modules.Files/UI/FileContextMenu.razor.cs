using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the file/folder context menu component.
/// </summary>
public partial class FileContextMenu : ComponentBase
{
    /// <summary>Whether the context menu is visible.</summary>
    [Parameter] public bool IsVisible { get; set; }

    /// <summary>X position in pixels (viewport coordinates).</summary>
    [Parameter] public double X { get; set; }

    /// <summary>Y position in pixels (viewport coordinates).</summary>
    [Parameter] public double Y { get; set; }

    /// <summary>ID of the target node.</summary>
    [Parameter] public Guid NodeId { get; set; }

    /// <summary>Type of the target node ("File" or "Folder").</summary>
    [Parameter] public string NodeType { get; set; } = "File";

    /// <summary>Raised when the user selects "Open".</summary>
    [Parameter] public EventCallback<Guid> OnOpen { get; set; }

    /// <summary>Raised when the user selects "Rename".</summary>
    [Parameter] public EventCallback<Guid> OnRename { get; set; }

    /// <summary>Raised when the user selects "Move to…".</summary>
    [Parameter] public EventCallback<Guid> OnMove { get; set; }

    /// <summary>Raised when the user selects "Copy to…".</summary>
    [Parameter] public EventCallback<Guid> OnCopy { get; set; }

    /// <summary>Raised when the user selects "Share".</summary>
    [Parameter] public EventCallback<Guid> OnShare { get; set; }

    /// <summary>Raised when the user selects "Download".</summary>
    [Parameter] public EventCallback<Guid> OnDownload { get; set; }

    /// <summary>Raised when the user selects "Delete".</summary>
    [Parameter] public EventCallback<Guid> OnDelete { get; set; }

    /// <summary>Raised when the menu should be dismissed.</summary>
    [Parameter] public EventCallback OnDismiss { get; set; }

    private async Task HandleOpen() => await OnOpen.InvokeAsync(NodeId);
    private async Task HandleRename() => await OnRename.InvokeAsync(NodeId);
    private async Task HandleMove() => await OnMove.InvokeAsync(NodeId);
    private async Task HandleCopy() => await OnCopy.InvokeAsync(NodeId);
    private async Task HandleShare() => await OnShare.InvokeAsync(NodeId);
    private async Task HandleDownload() => await OnDownload.InvokeAsync(NodeId);
    private async Task HandleDelete() => await OnDelete.InvokeAsync(NodeId);

    /// <summary>Handles keyboard navigation within the menu.</summary>
    protected async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            await OnDismiss.InvokeAsync();
        }
    }
}
