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

namespace DotNetCloud.UI.Shared.Components.DataDisplay;

/// <summary>Code-behind for the <c>DncDataTable&lt;TItem&gt;</c> component.</summary>
public partial class DncDataTable<TItem> : ComponentBase
{
    /// <summary>The column definitions.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<DataTableColumn<TItem>> Columns { get; set; } = [];

    /// <summary>The data items to display.</summary>
    [Parameter]
    public IEnumerable<TItem>? Items { get; set; }

    /// <summary>Text shown when no items are present.</summary>
    [Parameter]
    public string? EmptyText { get; set; }

    /// <summary>Function to compute a CSS class per row.</summary>
    [Parameter]
    public Func<TItem, string?>? RowClass { get; set; }

    /// <summary>Raised when a row is clicked.</summary>
    [Parameter]
    public EventCallback<TItem> OnRowClick { get; set; }

    /// <summary>Current sort column key.</summary>
    [Parameter]
    public string? CurrentSortKey { get; set; }

    /// <summary>Whether current sort is ascending.</summary>
    [Parameter]
    public bool SortAscending { get; set; } = true;

    /// <summary>Raised when a sortable column header is clicked.</summary>
    [Parameter]
    public EventCallback<(string SortKey, bool Ascending)> OnSortChanged { get; set; }

    /// <summary>Whether to display integrated pagination.</summary>
    [Parameter]
    public bool ShowPagination { get; set; }

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

    private async Task OnSort(DataTableColumn<TItem> column)
    {
        if (column.SortKey is null) return;

        if (CurrentSortKey == column.SortKey)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            CurrentSortKey = column.SortKey;
            SortAscending = true;
        }

        await OnSortChanged.InvokeAsync((CurrentSortKey, SortAscending));
    }
}
