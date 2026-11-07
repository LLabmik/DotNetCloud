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
    private readonly Dictionary<string, ModuleAppDescriptor> _apps = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Raised when the registry changes.
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    /// Gets the registered navigation items from all modules.
    /// </summary>
    public IReadOnlyList<ModuleNavItem> NavItems => _navItems;

    /// <summary>
    /// Gets the iframe-hosted module apps registered from their manifests.
    /// </summary>
    public IReadOnlyCollection<ModuleAppDescriptor> Apps => _apps.Values;

    /// <summary>
    /// Registers (or re-registers, idempotently) a navigation item for a module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="label">Display label in the sidebar.</param>
    /// <param name="href">Navigation URL.</param>
    /// <param name="icon">
    /// A Google Material Icons ligature name (for example <c>folder</c>). Never an emoji: the shell
    /// renders it through <c>MaterialIcon</c>.
    /// </param>
    /// <param name="sortOrder">Sort order (lower values appear first).</param>
    public void RegisterNavItem(string moduleId, string label, string href, string icon, int sortOrder = 100)
    {
        _navItems.RemoveAll(n => n.ModuleId == moduleId && string.Equals(n.Href, href, StringComparison.OrdinalIgnoreCase));
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
    /// Registers (or replaces, idempotently) an iframe-hosted module app.
    /// </summary>
    /// <param name="app">The descriptor read from the module's v2 manifest.</param>
    public void RegisterApp(ModuleAppDescriptor app)
    {
        ArgumentNullException.ThrowIfNull(app);
        _apps[app.ModuleId] = app;
        OnChange?.Invoke();
    }

    /// <summary>
    /// Gets the iframe app descriptor for a module, or <see langword="null"/> when the module does
    /// not host an iframe UI.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <returns>The descriptor, or <see langword="null"/>.</returns>
    public ModuleAppDescriptor? GetApp(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return null;
        }

        return _apps.TryGetValue(moduleId, out var app) ? app : null;
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
        _apps.Remove(moduleId);
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
/// <param name="ModuleId">The owning module id.</param>
/// <param name="Label">Display label.</param>
/// <param name="Href">Navigation URL.</param>
/// <param name="Icon">Material Icons ligature name (never an emoji).</param>
/// <param name="SortOrder">Sort order (lower values appear first).</param>
public sealed record ModuleNavItem(
    string ModuleId,
    string Label,
    string Href,
    string Icon,
    int SortOrder);

/// <summary>
/// Describes a module UI hosted by the module's own process and framed by the core.
/// </summary>
/// <param name="ModuleId">The module id (reverse-DNS, e.g. <c>org.example.customapp</c>).</param>
/// <param name="Title">Display title from the manifest.</param>
/// <param name="BasePath">Proxy base path, e.g. <c>/apps/org.example.customapp</c>.</param>
/// <param name="EntryPath">Normalized manifest <c>ui.entryPath</c> (always rooted).</param>
/// <param name="SortOrder">Sort order (lower values appear first).</param>
public sealed record ModuleAppDescriptor(
    string ModuleId,
    string Title,
    string BasePath,
    string EntryPath,
    int SortOrder = 100)
{
    /// <summary>
    /// Gets the site-relative URL the iframe loads.
    /// </summary>
    public string EntryUrl => EntryPath == "/" ? BasePath + "/" : BasePath + EntryPath;
}
