using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Example.UI;

/// <summary>Code-behind for the <see cref="ExampleSidebar"/> component.</summary>
public partial class ExampleSidebar : ComponentBase
{
    private bool _sidebarCollapsed;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var stored = await JSRuntime.InvokeAsync<string?>("localStorage.getItem", "example_sidebar_collapsed");
            if (bool.TryParse(stored, out var collapsed))
            {
                _sidebarCollapsed = collapsed;
            }
        }
        catch
        {
            // localStorage not available during pre-rendering
        }
    }

    private async Task ToggleCollapsed()
    {
        _sidebarCollapsed = !_sidebarCollapsed;
        try
        {
            await JSRuntime.InvokeVoidAsync("localStorage.setItem", "example_sidebar_collapsed", _sidebarCollapsed.ToString());
        }
        catch
        {
            // Silently fail during pre-rendering
        }
    }

    private void NavigateToNotes()
    {
        // Already on the notes page; no navigation needed.
    }
}
