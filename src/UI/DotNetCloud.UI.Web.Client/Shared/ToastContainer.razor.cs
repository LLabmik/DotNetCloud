using DotNetCloud.UI.Web.Client.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Client.Shared;

/// <summary>Code-behind for the <see cref="ToastContainer"/> component.</summary>
public partial class ToastContainer : ComponentBase, IDisposable
{
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
