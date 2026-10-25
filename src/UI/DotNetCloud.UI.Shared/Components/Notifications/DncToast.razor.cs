using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Notifications;

/// <summary>Code-behind for the <see cref="DncToast"/> component.</summary>
public partial class DncToast : ComponentBase, IDisposable
{
    /// <summary>The toast service providing notification state.</summary>
    [Parameter, EditorRequired]
    public DncToastService ToastService { get; set; } = default!;

    protected override void OnInitialized()
    {
        ToastService.OnChange += OnToastChanged;
    }

    private void OnToastChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        ToastService.OnChange -= OnToastChanged;
    }
}
