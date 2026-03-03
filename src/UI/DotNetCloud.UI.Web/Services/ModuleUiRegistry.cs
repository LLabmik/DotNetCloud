using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Services;

/// <summary>
/// Manages dynamic module UI registration for the Blazor shell.
/// Modules register their navigation items and page components here.
/// </summary>
public sealed class ModuleUiRegistry
{
    private readonly List<ModuleNavItem> _navItems = [];
    private readonly Dictionary<string, Type> _modulePages = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Raised when the registry changes.
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    /// Gets the registered navigation items from all modules.
    /// </summary>
    public IReadOnlyList<ModuleNavItem> NavItems => _navItems;

    /// <summary>
    /// Registers a navigation item for a module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="label">Display label in the sidebar.</param>
    /// <param name="href">Navigation URL.</param>
    /// <param name="icon">Emoji or icon string.</param>
    /// <param name="sortOrder">Sort order (lower values appear first).</param>
    public void RegisterNavItem(string moduleId, string label, string href, string icon, int sortOrder = 100)
    {
        _navItems.Add(new ModuleNavItem(moduleId, label, href, icon, sortOrder));
        _navItems.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        OnChange?.Invoke();
    }

    /// <summary>
    /// Registers a Blazor page component type for a module.
    /// </summary>
    /// <param name="routeKey">A route key such as "files.browser".</param>
    /// <param name="componentType">The Razor component type to render.</param>
    public void RegisterPage(string routeKey, Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        _modulePages[routeKey] = componentType;
        OnChange?.Invoke();
    }

    /// <summary>
    /// Gets a registered module page component type, or null if not found.
    /// </summary>
    public Type? GetPage(string routeKey)
    {
        _modulePages.TryGetValue(routeKey, out var type);
        return type;
    }

    /// <summary>
    /// Removes all registrations for a specific module.
    /// </summary>
    public void UnregisterModule(string moduleId)
    {
        _navItems.RemoveAll(n => n.ModuleId == moduleId);
        var keysToRemove = _modulePages
            .Where(kv => kv.Key.StartsWith(moduleId + ".", StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in keysToRemove)
            _modulePages.Remove(key);
        OnChange?.Invoke();
    }
}

/// <summary>
/// Represents a navigation item registered by a module.
/// </summary>
public sealed record ModuleNavItem(
    string ModuleId,
    string Label,
    string Href,
    string Icon,
    int SortOrder);
