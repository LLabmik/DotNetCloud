using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Components.Layout;

/// <summary>Code-behind for the <see cref="NavMenu"/> component.</summary>
public partial class NavMenu : ComponentBase, IDisposable
{
    [Parameter]
    public bool Collapsed { get; set; }

    protected override void OnInitialized()
    {
        ModuleRegistry.OnChange += OnModuleRegistryChanged;
    }

    public void Dispose()
    {
        ModuleRegistry.OnChange -= OnModuleRegistryChanged;
    }

    // The registry is written by background services, so the event arrives on a non-dispatcher
    // thread. Subscribing StateHasChanged directly threw "The current thread is not associated
    // with the Dispatcher" the moment a module's UI registration changed while a page was open
    // (a module update), which aborted the registration and hid the app until a restart.
    private void OnModuleRegistryChanged() => _ = InvokeAsync(StateHasChanged);
}
