using System.Net.Http;
using System.Security.Claims;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.DTOs.Home;
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
using DotNetCloud.UI.Web.Client.Services;
using DotNetCloud.UI.Web.Client.Shared;
using DotNetCloud.UI.Web.Components.Layout;
using DotNetCloud.UI.Web.Components.Pages;
using DotNetCloud.UI.Web.Components.Pages.Modules.Help;
using DotNetCloud.UI.Web.Components.Shared;
using DotNetCloud.UI.Web.Components.Shared.Search;
using DotNetCloud.UI.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using static Microsoft.AspNetCore.Components.Web.RenderMode;

namespace DotNetCloud.UI.Web.Components.Home;

/// <summary>Code-behind for the <see cref="HomeWidgetCustomizer"/> component.</summary>
public partial class HomeWidgetCustomizer : ComponentBase, IDisposable
{
    /// <summary>The layout service that owns the user's preferences.</summary>
    [Inject]
    private HomeWidgetLayoutService LayoutService { get; set; } = default!;

    /// <summary>Raised when the user closes the panel.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    private string? _draggingModuleId;
    private string _status = "Drag to reorder, or use the arrows.";
    private bool _confirmingReset;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        LayoutService.OnChange += OnLayoutChanged;
        await LayoutService.EnsureLoadedAsync();
    }

    /// <inheritdoc />
    public void Dispose() => LayoutService.OnChange -= OnLayoutChanged;

    private void OnLayoutChanged() => _ = InvokeAsync(StateHasChanged);

    private bool IsDragging(HomeWidgetSlot slot)
        => _draggingModuleId is not null
            && string.Equals(_draggingModuleId, slot.ModuleId, StringComparison.OrdinalIgnoreCase);

    private Task CloseAsync() => OnClose.InvokeAsync();

    private async Task SelectStyleAsync(string token)
    {
        await LayoutService.SetStyleAsync(token);
        _status = $"Style set to {HomeWidgetStyles.GetDisplayName(token)}.";
    }

    private async Task SetVisibleAsync(HomeWidgetSlot slot, bool visible)
    {
        await LayoutService.SetVisibilityAsync(slot.ModuleId, visible);
        _status = visible
            ? $"{slot.Descriptor.Title} shown."
            : $"{slot.Descriptor.Title} hidden.";
    }

    /// <summary>
    /// Moves the widget one position up or down. The slot (not a loop index) identifies the widget:
    /// a lambda that captured the <c>@for</c> index would close over the loop variable and see its
    /// final value (the slot count) when the event finally fires.
    /// </summary>
    /// <param name="slot">The slot whose widget is being moved.</param>
    /// <param name="delta">-1 to move up, +1 to move down.</param>
    private async Task MoveAsync(HomeWidgetSlot slot, int delta)
    {
        var currentIndex = LayoutService.GetPosition(slot.ModuleId);
        var targetIndex = currentIndex + delta;

        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= LayoutService.Slots.Count)
        {
            return;
        }

        await LayoutService.MoveAsync(slot.ModuleId, delta);
        _status = $"{slot.Descriptor.Title} moved to position {targetIndex + 1}.";
    }

    /// <summary>
    /// Moves the dragged widget into the dropped-on widget's slot. Both ends are module ids, so a
    /// re-render between drag start and drop cannot leave a stale index behind.
    /// </summary>
    /// <param name="targetModuleId">The module whose row the drag ended on.</param>
    private async Task DropAsync(string targetModuleId)
    {
        if (_draggingModuleId is not { } draggedModuleId
            || string.Equals(draggedModuleId, targetModuleId, StringComparison.OrdinalIgnoreCase))
        {
            _draggingModuleId = null;
            return;
        }

        var title = LayoutService.Slots
            .FirstOrDefault(candidate => string.Equals(
                candidate.ModuleId,
                draggedModuleId,
                StringComparison.OrdinalIgnoreCase))
            ?.Descriptor.Title ?? draggedModuleId;

        await LayoutService.MoveToModuleAsync(draggedModuleId, targetModuleId);

        var position = LayoutService.GetPosition(draggedModuleId);
        _draggingModuleId = null;
        _status = position >= 0
            ? $"{title} moved to position {position + 1}."
            : $"{title} reordered.";
    }

    private async Task ConfirmResetAsync()
    {
        if (!_confirmingReset)
        {
            _confirmingReset = true;
            _status = "Reset restores the default style, order and visibility.";
            return;
        }

        _confirmingReset = false;
        await LayoutService.ResetAsync();
        _status = "Layout reset to default.";
    }
}
