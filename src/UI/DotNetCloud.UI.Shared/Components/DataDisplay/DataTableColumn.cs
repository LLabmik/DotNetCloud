using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.DataDisplay;

/// <summary>
/// Defines a column in a <see cref="DncDataTable{TItem}"/>.
/// </summary>
/// <typeparam name="TItem">The type of each row item.</typeparam>
public sealed class DataTableColumn<TItem>
{
    /// <summary>Column header text.</summary>
    public required string Title { get; init; }

    /// <summary>Function to extract the cell value from a row item.</summary>
    public required Func<TItem, object?> Field { get; init; }

    /// <summary>Property name used for sorting. If null, sorting is disabled for this column.</summary>
    public string? SortKey { get; init; }

    /// <summary>Optional CSS class applied to each cell in this column.</summary>
    public string? CssClass { get; init; }

    /// <summary>Optional render fragment for custom cell rendering.</summary>
    public RenderFragment<TItem>? Template { get; init; }
}
