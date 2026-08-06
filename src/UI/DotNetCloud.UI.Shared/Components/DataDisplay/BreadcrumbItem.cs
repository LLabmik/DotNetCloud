namespace DotNetCloud.UI.Shared.Components.DataDisplay;

/// <summary>
/// Represents a single breadcrumb item.
/// </summary>
/// <param name="Label">Display text for the breadcrumb.</param>
/// <param name="Href">Navigation URL. Null for the current (last) item.</param>
/// <param name="OnClick">Optional click handler for in-app (state-based) navigation.
/// When set, the item renders as a clickable button that invokes this callback instead
/// of navigating via <paramref name="Href"/>. Null for the current (last) item.</param>
public sealed record BreadcrumbItem(string Label, string? Href = null, Func<Task>? OnClick = null)
{
    /// <summary>
    /// Creates a breadcrumb item with a synchronous click handler.
    /// </summary>
    /// <param name="Label">Display text for the breadcrumb.</param>
    /// <param name="onClick">Synchronous action invoked when the item is clicked.</param>
    public BreadcrumbItem(string Label, Action onClick)
        : this(Label, null, () => { onClick(); return Task.CompletedTask; })
    {
    }

    /// <summary>
    /// Creates a breadcrumb item with an asynchronous click handler.
    /// </summary>
    /// <param name="Label">Display text for the breadcrumb.</param>
    /// <param name="onClick">Asynchronous action invoked when the item is clicked.</param>
    public BreadcrumbItem(string Label, Func<Task> onClick)
        : this(Label, null, onClick)
    {
    }
}
