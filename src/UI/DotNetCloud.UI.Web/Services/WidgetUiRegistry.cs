using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Services;

/// <summary>
/// Manages widget component registration for the Blazor shell.
/// Modules register a Home-page widget card that shows recent/informative data.
/// </summary>
public sealed class WidgetUiRegistry
{
    private readonly List<WidgetDescriptor> _widgets = [];

    /// <summary>
    /// Raised when the widget registration set changes.
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    /// Gets the registered widgets, ordered by <see cref="WidgetDescriptor.SortOrder"/>.
    /// </summary>
    public IReadOnlyList<WidgetDescriptor> Widgets => _widgets;

    /// <summary>
    /// Registers (or re-registers, idempotently) a widget for a module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="title">Display title of the widget card.</param>
    /// <param name="icon">Material icon ligature name.</param>
    /// <param name="href">"Open" link target.</param>
    /// <param name="componentType">The Blazor component type that renders the widget body.</param>
    /// <param name="sortOrder">Sort order (lower values appear first).</param>
    public void RegisterWidget(string moduleId, string title, string icon, string href, Type componentType, int sortOrder = 100)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        _widgets.RemoveAll(w => w.ModuleId == moduleId);   // idempotent re-registration
        _widgets.Add(new WidgetDescriptor(moduleId, title, icon, href, componentType, sortOrder));
        _widgets.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        OnChange?.Invoke();
    }

    /// <summary>
    /// Removes all widgets registered for a specific module.
    /// </summary>
    public void UnregisterModule(string moduleId)
    {
        _widgets.RemoveAll(w => w.ModuleId == moduleId);
        OnChange?.Invoke();
    }
}

/// <summary>
/// Describes a widget registered by a module.
/// </summary>
public sealed record WidgetDescriptor(
    string ModuleId,
    string Title,
    string Icon,
    string Href,
    Type ComponentType,
    int SortOrder);
