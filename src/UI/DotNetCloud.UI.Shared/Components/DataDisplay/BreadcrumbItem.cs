namespace DotNetCloud.UI.Shared.Components.DataDisplay;

/// <summary>
/// Represents a single breadcrumb item.
/// </summary>
/// <param name="Label">Display text for the breadcrumb.</param>
/// <param name="Href">Navigation URL. Null for the current (last) item.</param>
/// <param name="OnClick">Optional click handler for in-app (state-based) navigation.
/// When set, the item renders as a clickable button that invokes this callback instead
/// of navigating via <paramref name="Href"/>. Null for the current (last) item.</param>
public sealed record BreadcrumbItem(string Label, string? Href = null, Func<Task>? OnClick = null);
