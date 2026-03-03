namespace DotNetCloud.UI.Shared.Components.DataDisplay;

/// <summary>
/// Represents a single breadcrumb item.
/// </summary>
/// <param name="Label">Display text for the breadcrumb.</param>
/// <param name="Href">Navigation URL. Null for the current (last) item.</param>
public sealed record BreadcrumbItem(string Label, string? Href = null);
