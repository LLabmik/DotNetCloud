using DotNetCloud.Core;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Ui;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.UI.Shared.Services;
using DotNetCloud.UI.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Initialization;

/// <summary>
/// Registers the navigation entries and iframe descriptors of modules that declare
/// <c>ui.type = "iframe"</c> in their version 2 manifest.
/// </summary>
/// <remarks>
/// <para>
/// This is the manifest-driven half of the UI plane. <see cref="ModuleUiRegistrationHostedService"/>
/// keeps owning the first-party modules, whose Blazor pages are compiled into the core; a module
/// that ships its own UI from its own process is registered here instead.
/// </para>
/// <para>
/// Required modules are excluded by contract (the manifest validator rejects a <c>ui</c> block for
/// them), so the two paths cannot register the same id.
/// </para>
/// <para>
/// Note: <c>dotnetcloud.example</c> and <c>dotnetcloud.bookmarks</c> are the first in-repo modules
/// with a v2 manifest that declares a UI; the rest are migrated by Track C. Until then the service
/// is exercised mostly by unit tests against a fake manifest registry.
/// </para>
/// </remarks>
internal sealed class ModuleAppRegistrationHostedService : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(15);

    /// <summary>Suggested sort base for manifest-declared apps (after the first-party modules).</summary>
    internal const int DefaultSortOrder = 500;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IModuleManifestRegistry _manifestRegistry;
    private readonly ModuleUiRegistry _moduleUiRegistry;
    private readonly WidgetUiRegistry _widgetUiRegistry;
    private readonly ILogger<ModuleAppRegistrationHostedService> _logger;

    private readonly Dictionary<string, string> _registered = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleAppRegistrationHostedService"/> class.
    /// </summary>
    /// <param name="scopeFactory">Scope factory for database reads.</param>
    /// <param name="manifestRegistry">Registry of manifests recorded by the supervisor.</param>
    /// <param name="moduleUiRegistry">The shell's navigation/page registry.</param>
    /// <param name="widgetUiRegistry">The shell's widget registry.</param>
    /// <param name="logger">Logger.</param>
    public ModuleAppRegistrationHostedService(
        IServiceScopeFactory scopeFactory,
        IModuleManifestRegistry manifestRegistry,
        ModuleUiRegistry moduleUiRegistry,
        WidgetUiRegistry widgetUiRegistry,
        ILogger<ModuleAppRegistrationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _manifestRegistry = manifestRegistry;
        _moduleUiRegistry = moduleUiRegistry;
        _widgetUiRegistry = widgetUiRegistry;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshAsync(stoppingToken);

        using var timer = new PeriodicTimer(RefreshInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RefreshAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Refreshing manifest-declared module UIs failed.");
            }
        }
    }

    /// <summary>
    /// Reconciles the registry against the enabled modules that declare an iframe UI.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task RefreshAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var statuses = await dbContext.InstalledModules
            .AsNoTracking()
            .ToDictionaryAsync(m => m.ModuleId, m => m.Status, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var moduleId in _manifestRegistry.GetModuleIds())
        {
            if (!statuses.TryGetValue(moduleId, out var status)
                || !string.Equals(status, "Enabled", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var manifest = _manifestRegistry.GetManifest(moduleId);
            if (manifest is null
                || manifest.Ui is not { } ui
                || !string.Equals(ui.Type, "iframe", StringComparison.Ordinal)
                || RequiredModules.IsRequired(moduleId))
            {
                continue;
            }

            seen.Add(moduleId);
            var fingerprint = BuildFingerprint(manifest, ui);

            if (_registered.TryGetValue(moduleId, out var previous)
                && string.Equals(previous, fingerprint, StringComparison.Ordinal))
            {
                continue;
            }

            Apply(moduleId, manifest, ui);
            _registered[moduleId] = fingerprint;
        }

        // Forget modules that were disabled, uninstalled or stopped declaring a UI.
        foreach (var moduleId in _registered.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            _moduleUiRegistry.UnregisterModule(moduleId);
            _widgetUiRegistry.UnregisterModule(moduleId);
            _registered.Remove(moduleId);
            _logger.LogInformation("Unregistered module UI for {ModuleId}", LogSanitizer.Sanitize(moduleId));
        }
    }

    private void Apply(string moduleId, ModuleManifestData manifest, ManifestUi ui)
    {
        var basePath = ModuleAppUrls.BasePath(moduleId);
        if (basePath.Length == 0)
        {
            _logger.LogWarning("Skipping module UI registration for an invalid module id.");
            return;
        }

        var shellPath = ModuleAppUrls.ShellPath(moduleId);
        var entryPath = ModuleAppUrls.NormalizeEntryPath(ui.EntryPath);
        var canonicalIcon = ModuleIconProvider.GetIcon(moduleId, "widgets");

        // The nav set for this module is owned entirely by the manifest, so drop stale entries.
        _moduleUiRegistry.UnregisterModule(moduleId);
        _widgetUiRegistry.UnregisterModule(moduleId);

        _moduleUiRegistry.RegisterApp(new ModuleAppDescriptor(
            moduleId,
            manifest.Name,
            basePath,
            entryPath,
            DefaultSortOrder));

        var sortOrder = DefaultSortOrder;
        foreach (var navItem in ui.NavItems)
        {
            // Navigation goes to the core-chrome shell page, which frames the proxy path. The shell
            // route has no trailing-slash variant, so "/" maps to the bare shell path.
            var relative = ModuleAppUrls.NormalizeEntryPath(navItem.Path);
            var href = relative == "/" ? shellPath : shellPath + relative;
            var icon = ModuleIconProvider.ResolveIcon(navItem.Icon, canonicalIcon);

            _moduleUiRegistry.RegisterNavItem(moduleId, navItem.Label, href, icon, sortOrder);
            sortOrder++;
        }

        // A module that declares navItems but no explicit path still needs a way into its app.
        if (ui.NavItems.Count == 0)
        {
            _moduleUiRegistry.RegisterNavItem(
                moduleId, manifest.Name, shellPath, canonicalIcon, DefaultSortOrder);
        }

        // A home-page widget is opt-in: the shell never guesses a path, because a module's full app
        // is usually the wrong thing to embed in a card. The frame loads the declared page inside
        // the same sandbox and message contract as the app.
        if (!string.IsNullOrWhiteSpace(ui.Widget?.Path))
        {
            _widgetUiRegistry.RegisterIframeWidget(
                moduleId,
                manifest.Name,
                canonicalIcon,
                shellPath,
                ModuleAppUrls.NormalizeEntryPath(ui.Widget.Path),
                DefaultSortOrder);
        }

        _logger.LogInformation(
            "Registered iframe UI for {ModuleId} at {BasePath} ({NavItems} nav item(s), widget: {HasWidget})",
            LogSanitizer.Sanitize(moduleId),
            basePath,
            ui.NavItems.Count,
            ui.Widget is not null);
    }

    private static string BuildFingerprint(ModuleManifestData manifest, ManifestUi ui)
    {
        var navItems = string.Join(
            '\u001f',
            ui.NavItems.Select(item => $"{item.Id}|{item.Label}|{item.Icon}|{item.Path}"));

        return $"{manifest.Version}\u001e{ui.EntryPath}\u001e{manifest.Name}\u001e{ui.Widget?.Path}\u001e{navItems}";
    }
}
