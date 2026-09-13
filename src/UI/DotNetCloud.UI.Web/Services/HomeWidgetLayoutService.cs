using System.Security.Claims;
using DotNetCloud.Core.DTOs.Home;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.UI.Web.Services;

/// <summary>
/// One Home-page widget slot: the registered widget plus whether the user wants it shown.
/// </summary>
/// <param name="Descriptor">The registered widget.</param>
/// <param name="Visible">Whether the user wants the widget rendered.</param>
public sealed record HomeWidgetSlot(WidgetDescriptor Descriptor, bool Visible)
{
    /// <summary>
    /// Gets the owning module identifier.
    /// </summary>
    public string ModuleId => Descriptor.ModuleId;
}

/// <summary>
/// Owns the signed-in user's Home-page widget layout: the active style, the display order, and which
/// widgets are hidden. Caches the loaded preferences for the lifetime of the circuit and serialises
/// writes so rapid edits cannot race each other.
/// </summary>
public sealed class HomeWidgetLayoutService : IDisposable
{
    private readonly WidgetUiRegistry _registry;
    private readonly IUserSettingsService _settings;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILogger<HomeWidgetLayoutService> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private HomeWidgetPreferences? _preferences;

    /// <summary>
    /// Initializes a new instance of the <see cref="HomeWidgetLayoutService"/> class.
    /// </summary>
    /// <param name="registry">The registered widget catalog.</param>
    /// <param name="settings">The per-user settings store.</param>
    /// <param name="authStateProvider">Supplies the signed-in user identity.</param>
    /// <param name="logger">The logger.</param>
    public HomeWidgetLayoutService(
        WidgetUiRegistry registry,
        IUserSettingsService settings,
        AuthenticationStateProvider authStateProvider,
        ILogger<HomeWidgetLayoutService> logger)
    {
        _registry = registry;
        _settings = settings;
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    /// <summary>
    /// Raised whenever the layout or style changes, so the UI can re-render.
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    /// Gets every registered widget in the user's order, including hidden ones.
    /// </summary>
    public IReadOnlyList<HomeWidgetSlot> Slots { get; private set; } = [];

    /// <summary>
    /// Gets the widgets to render, in order.
    /// </summary>
    public IReadOnlyList<HomeWidgetSlot> VisibleSlots => Slots.Where(slot => slot.Visible).ToList();

    /// <summary>
    /// Gets the active style token.
    /// </summary>
    public string StyleToken => HomeWidgetStyles.Normalize(_preferences?.Style);

    /// <summary>
    /// Gets a value indicating whether the user's preferences have been loaded.
    /// </summary>
    public bool IsLoaded { get; private set; }

    /// <summary>
    /// Gets the most recent user-facing error, if the last save failed.
    /// </summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// Loads the user's preferences once per circuit and materialises the slot list.
    /// Safe to call repeatedly (Home is prerendered, so initialization runs more than once).
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoaded)
        {
            return;
        }

        IsLoaded = true;

        var userId = await TryGetUserIdAsync();
        if (userId is null)
        {
            RecomputeSlots();
            return;
        }

        try
        {
            _preferences = await HomeWidgetPreferencesSettings.LoadAsync(_settings, userId.Value);
        }
        catch (Exception ex)
        {
            // A settings read failure must not take the home page down - fall back to defaults.
            _logger.LogError(ex, "Failed to load home widget preferences for user {UserId}.", userId);
            _preferences = new HomeWidgetPreferences();
        }

        RecomputeSlots();
        NotifyChanged();
    }

    /// <summary>
    /// Rebuilds the slot list from the current registry. Call when the registry changes (a module
    /// was installed or removed) so newly registered widgets appear without a reload.
    /// </summary>
    public void RefreshFromRegistry()
    {
        RecomputeSlots();
        NotifyChanged();
    }

    /// <summary>
    /// Applies a style token and persists it.
    /// </summary>
    /// <param name="styleToken">The style token to apply.</param>
    /// <returns><see langword="true"/> when the change was persisted.</returns>
    public Task<bool> SetStyleAsync(string styleToken)
        => MutateAsync(() => Rebuild(styleToken: styleToken));

    /// <summary>
    /// Shows or hides one widget and persists the change.
    /// </summary>
    /// <param name="moduleId">The module that owns the widget.</param>
    /// <param name="visible">Whether the widget should be shown.</param>
    /// <returns><see langword="true"/> when the change was persisted.</returns>
    public Task<bool> SetVisibilityAsync(string moduleId, bool visible)
        => MutateAsync(() =>
        {
            var layout = CurrentLayoutItems()
                .Select(item => string.Equals(item.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase)
                    ? item with { Visible = visible }
                    : item)
                .ToList();

            return Rebuild(layout: layout);
        });

    /// <summary>
    /// Moves a widget one position up or down in the display order and persists the change.
    /// This is the keyboard-accessible counterpart to drag and drop.
    /// </summary>
    /// <param name="moduleId">The module that owns the widget.</param>
    /// <param name="delta">-1 to move up, +1 to move down.</param>
    /// <returns><see langword="true"/> when the change was persisted.</returns>
    public Task<bool> MoveAsync(string moduleId, int delta)
        => MutateAsync(() =>
        {
            var layout = CurrentLayoutItems();
            var from = IndexOf(layout, moduleId);

            return from < 0
                ? Rebuild(layout: layout)
                : Rebuild(layout: Move(layout, from, from + delta));
        });

    /// <summary>
    /// Moves a widget to the slot currently held by another widget and persists the change. Used by
    /// drag and drop: both ends are identified by module id rather than by index, so a stale index
    /// (the list can be re-rendered between drag start and drop) can never move the wrong widget or
    /// throw.
    /// </summary>
    /// <param name="moduleId">The module that owns the widget being dragged.</param>
    /// <param name="targetModuleId">The module whose slot the widget was dropped on.</param>
    /// <returns><see langword="true"/> when the change was persisted.</returns>
    public Task<bool> MoveToModuleAsync(string moduleId, string targetModuleId)
        => MutateAsync(() =>
        {
            var layout = CurrentLayoutItems();
            var from = IndexOf(layout, moduleId);
            var to = IndexOf(layout, targetModuleId);

            return from < 0 || to < 0
                ? Rebuild(layout: layout)
                : Rebuild(layout: Move(layout, from, to));
        });

    /// <summary>
    /// Gets the current zero-based display position of a widget, or <c>-1</c> when the module has no
    /// widget slot.
    /// </summary>
    /// <param name="moduleId">The module that owns the widget.</param>
    /// <returns>The zero-based position, or <c>-1</c>.</returns>
    public int GetPosition(string moduleId) => IndexOf(CurrentLayoutItems(), moduleId);

    /// <summary>
    /// Restores the default style, registry order and full visibility, and persists it.
    /// </summary>
    /// <returns><see langword="true"/> when the reset was persisted.</returns>
    public Task<bool> ResetAsync()
        => MutateAsync(() => new HomeWidgetPreferences
        {
            Version = HomeWidgetPreferences.CurrentVersion,
            Style = HomeWidgetStyles.DefaultToken,
            Items = [],
        });

    /// <summary>
    /// Clears the last error, so the UI can dismiss its message.
    /// </summary>
    public void ClearError()
    {
        if (LastError is null)
        {
            return;
        }

        LastError = null;
        NotifyChanged();
    }

    /// <inheritdoc />
    public void Dispose() => _writeLock.Dispose();

    private static IReadOnlyList<HomeWidgetLayoutItem> Move(
        IReadOnlyList<HomeWidgetLayoutItem> layout,
        int fromIndex,
        int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= layout.Count || toIndex < 0 || toIndex >= layout.Count || fromIndex == toIndex)
        {
            return layout;
        }

        var list = layout.ToList();
        var moved = list[fromIndex];
        list.RemoveAt(fromIndex);
        list.Insert(toIndex, moved);
        return list;
    }

    private static int IndexOf(IReadOnlyList<HomeWidgetLayoutItem> layout, string moduleId)
    {
        for (var i = 0; i < layout.Count; i++)
        {
            if (string.Equals(layout[i].ModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private IReadOnlyList<HomeWidgetLayoutItem> CurrentLayoutItems()
        => Slots.Select(slot => new HomeWidgetLayoutItem(slot.ModuleId, slot.Visible)).ToList();

    private HomeWidgetPreferences Rebuild(
        string? styleToken = null,
        IReadOnlyList<HomeWidgetLayoutItem>? layout = null)
        => HomeWidgetLayoutResolver.BuildPreferences(
            styleToken ?? StyleToken,
            layout ?? CurrentLayoutItems(),
            _preferences);

    /// <summary>
    /// Applies a mutation optimistically (so the UI never waits on the round-trip), then persists it.
    /// A failed write leaves the local state in place and surfaces an error; the next load reverts to
    /// the last successfully persisted layout.
    /// </summary>
    private async Task<bool> MutateAsync(Func<HomeWidgetPreferences> mutate)
    {
        var userId = await TryGetUserIdAsync();
        if (userId is null)
        {
            LastError = "Couldn't save your widget layout because you aren't signed in.";
            NotifyChanged();
            return false;
        }

        var updated = mutate();
        _preferences = updated;
        RecomputeSlots();
        NotifyChanged();

        await _writeLock.WaitAsync();
        try
        {
            await HomeWidgetPreferencesSettings.SaveAsync(_settings, userId.Value, updated);
            LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save home widget preferences for user {UserId}.", userId);
            LastError = "Couldn't save your widget layout. Your change is applied for now but may not stick.";
            return false;
        }
        finally
        {
            _writeLock.Release();
            NotifyChanged();
        }
    }

    private void RecomputeSlots()
    {
        var descriptors = _registry.Widgets;
        var catalog = descriptors
            .Select(descriptor => new HomeWidgetCatalogEntry(descriptor.ModuleId, descriptor.SortOrder))
            .ToList();

        var byModuleId = new Dictionary<string, WidgetDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in descriptors)
        {
            byModuleId[descriptor.ModuleId] = descriptor;
        }

        Slots = HomeWidgetLayoutResolver
            .Resolve(catalog, _preferences)
            .Where(item => byModuleId.ContainsKey(item.ModuleId))
            .Select(item => new HomeWidgetSlot(byModuleId[item.ModuleId], item.Visible))
            .ToList();
    }

    private async Task<Guid?> TryGetUserIdAsync()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var claimValue = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? authState.User.FindFirst("sub")?.Value;

        return Guid.TryParse(claimValue, out var userId) ? userId : null;
    }

    private void NotifyChanged() => OnChange?.Invoke();
}
