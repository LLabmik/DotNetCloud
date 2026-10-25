using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.DataDisplay;

/// <summary>Code-behind for the <see cref="DncPaginator"/> component.</summary>
public partial class DncPaginator : ComponentBase
{
    /// <summary>Current page number (1-based).</summary>
    [Parameter]
    public int CurrentPage { get; set; } = 1;

    /// <summary>Total number of pages.</summary>
    [Parameter]
    public int TotalPages { get; set; }

    /// <summary>Total number of items across all pages.</summary>
    [Parameter]
    public int TotalCount { get; set; }

    /// <summary>Raised when the page changes.</summary>
    [Parameter]
    public EventCallback<int> OnPageChanged { get; set; }

    private async Task GoToPage(int page)
    {
        if (page < 1 || page > TotalPages) return;
        await OnPageChanged.InvokeAsync(page);
    }
}
