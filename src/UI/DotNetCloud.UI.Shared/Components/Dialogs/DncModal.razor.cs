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

/// <summary>Code-behind for the <see cref="DncModal"/> component.</summary>
public partial class DncModal : ComponentBase
{
    private bool _visible;

    /// <summary>Dialog title displayed in the header.</summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>Body content of the dialog.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Footer content (typically action buttons).</summary>
    [Parameter]
    public RenderFragment? Footer { get; set; }

    /// <summary>Whether to use the wider dialog variant (560px).</summary>
    [Parameter]
    public bool Wide { get; set; }

    /// <summary>Whether to use the extra-wide dialog variant (800px).</summary>
    [Parameter]
    public bool ExtraWide { get; set; }

    /// <summary>Additional CSS class for the dialog container.</summary>
    [Parameter]
    public string? CssClass { get; set; }

    /// <summary>Whether clicking the overlay closes the dialog.</summary>
    [Parameter]
    public bool CloseOnOverlayClick { get; set; } = true;

    /// <summary>
    /// When true the dialog is visible immediately upon rendering.
    /// Useful when the parent conditionally renders the modal with <c>@if</c>.
    /// </summary>
    [Parameter]
    public bool StartVisible { get; set; }

    /// <summary>Raised when the dialog is closed.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (StartVisible)
        {
            _visible = true;
        }
    }

    /// <summary>Shows the dialog.</summary>
    public void Show()
    {
        _visible = true;
        StateHasChanged();
    }

    /// <summary>Closes the dialog.</summary>
    public async Task CloseAsync()
    {
        _visible = false;
        await OnClose.InvokeAsync();
        StateHasChanged();
    }

    private async Task OnOverlayClick()
    {
        if (CloseOnOverlayClick)
        {
            await CloseAsync();
        }
    }
}
