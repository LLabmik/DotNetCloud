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
    /// Registers (or re-registers, idempotently) a widget whose body is a sandboxed iframe served
    /// by the module's own host process through the core's <c>/apps/{moduleId}/**</c> proxy.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="title">Display title of the widget card.</param>
    /// <param name="icon">Material icon ligature name.</param>
    /// <param name="href">"Open" link target (the module's full-page app).</param>
    /// <param name="appPath">Module-relative path the widget frame loads, e.g. <c>/widget</c>.</param>
    /// <param name="sortOrder">Sort order (lower values appear first).</param>
    public void RegisterIframeWidget(
        string moduleId,
        string title,
        string icon,
        string href,
        string appPath,
        int sortOrder = 100)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        _widgets.RemoveAll(w => w.ModuleId == moduleId);
        _widgets.Add(new WidgetDescriptor(
            moduleId,
            title,
            icon,
            href,
            ComponentType: null,
            sortOrder,
            DotNetCloud.Core.Modules.Ui.ModuleAppUrls.NormalizeEntryPath(appPath)));
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
/// <param name="ModuleId">The owning module id.</param>
/// <param name="Title">Display title of the card.</param>
/// <param name="Icon">Material icon ligature name.</param>
/// <param name="Href">"Open" link target.</param>
/// <param name="ComponentType">
/// The Blazor component that renders the body, or <see langword="null"/> for an iframe widget.
/// </param>
/// <param name="SortOrder">Sort order (lower values appear first).</param>
/// <param name="IframePath">
/// Module-relative path served by the module's own process; set only when
/// <paramref name="ComponentType"/> is <see langword="null"/>.
/// </param>
public sealed record WidgetDescriptor(
    string ModuleId,
    string Title,
    string Icon,
    string Href,
    Type? ComponentType,
    int SortOrder,
    string? IframePath = null)
{
    /// <summary>
    /// Gets a value indicating whether this widget's body is a sandboxed iframe.
    /// </summary>
    public bool IsIframe => ComponentType is null && !string.IsNullOrEmpty(IframePath);
}
