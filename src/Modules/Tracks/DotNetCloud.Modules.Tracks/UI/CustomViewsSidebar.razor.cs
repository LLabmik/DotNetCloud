using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Sidebar section showing saved custom views for a product.
/// </summary>
public partial class CustomViewsSidebar : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    /// <summary>The product to show views for.</summary>
    [Parameter] public Guid ProductId { get; set; }

    /// <summary>Whether the sidebar is collapsed.</summary>
    [Parameter] public bool Collapsed { get; set; }

    /// <summary>Currently selected view ID.</summary>
    [Parameter] public Guid? SelectedViewId { get; set; }

    /// <summary>Called when a saved view is clicked.</summary>
    [Parameter] public EventCallback<CustomViewDto> OnViewSelected { get; set; }

    /// <summary>Called when user wants to save the current view.</summary>
    [Parameter] public EventCallback OnSaveCurrentView { get; set; }

    private readonly List<CustomViewDto> _views = [];

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await LoadViewsAsync();
    }

    /// <summary>Reloads saved views from the API.</summary>
    public async Task LoadViewsAsync()
    {
        try
        {
            var views = await ApiClient.ListCustomViewsAsync(ProductId);
            _views.Clear();
            _views.AddRange(views);
        }
        catch
        {
            // Views failed to load — silently continue
        }
    }
}
