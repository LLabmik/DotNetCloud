using DotNetCloud.Core.Data.Context;
using DotNetCloud.UI.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Server.Initialization;

/// <summary>
/// Registers module navigation items and page components for installed modules.
/// </summary>
internal sealed class ModuleUiRegistrationHostedService : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(15);

    private static readonly ModuleUiDescriptor[] KnownModuleUiDescriptors =
    [
        new(
            ModuleId: "dotnetcloud.files",
            Label: "Files",
            Href: "/apps/files",
            Icon: "📁",
            SortOrder: 10,
            RouteKey: "files.browser",
            ComponentType: typeof(DotNetCloud.Modules.Files.UI.FileBrowser)),
        new(
            ModuleId: "dotnetcloud.chat",
            Label: "Chat",
            Href: "/apps/chat",
            Icon: "💬",
            SortOrder: 20,
            RouteKey: "chat.channels",
            ComponentType: typeof(DotNetCloud.Modules.Chat.UI.ChatPageLayout)),
        new(
            ModuleId: "dotnetcloud.contacts",
            Label: "Contacts",
            Href: "/apps/contacts",
            Icon: "👤",
            SortOrder: 30,
            RouteKey: "contacts.page",
            ComponentType: typeof(DotNetCloud.Modules.Contacts.UI.ContactsPage)),
        new(
            ModuleId: "dotnetcloud.calendar",
            Label: "Calendar",
            Href: "/apps/calendar",
            Icon: "📅",
            SortOrder: 40,
            RouteKey: "calendar.page",
            ComponentType: typeof(DotNetCloud.Modules.Calendar.UI.CalendarPage)),
        new(
            ModuleId: "dotnetcloud.notes",
            Label: "Notes",
            Href: "/apps/notes",
            Icon: "📝",
            SortOrder: 50,
            RouteKey: "notes.page",
            ComponentType: typeof(DotNetCloud.Modules.Notes.UI.NotesPage))
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ModuleUiRegistry _moduleUiRegistry;
    private readonly ILogger<ModuleUiRegistrationHostedService> _logger;
    private IReadOnlyDictionary<string, string> _lastInstalledModuleStatuses =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public ModuleUiRegistrationHostedService(
        IServiceScopeFactory scopeFactory,
        ModuleUiRegistry moduleUiRegistry,
        ILogger<ModuleUiRegistrationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _moduleUiRegistry = moduleUiRegistry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshModuleUiRegistrationAsync(stoppingToken);

        using var timer = new PeriodicTimer(RefreshInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshModuleUiRegistrationAsync(stoppingToken);
        }
    }

    private async Task RefreshModuleUiRegistrationAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var installedModuleStatuses = await dbContext.InstalledModules
            .AsNoTracking()
            .ToDictionaryAsync(m => m.ModuleId, m => m.Status, StringComparer.OrdinalIgnoreCase, cancellationToken);

        if (AreStatusesEqual(installedModuleStatuses, _lastInstalledModuleStatuses))
        {
            return;
        }

        foreach (var descriptor in KnownModuleUiDescriptors)
        {
            _moduleUiRegistry.UnregisterModule(descriptor.ModuleId);
        }

        foreach (var descriptor in KnownModuleUiDescriptors)
        {
            var isEnabled = installedModuleStatuses.TryGetValue(descriptor.ModuleId, out var status)
                && string.Equals(status, "Enabled", StringComparison.OrdinalIgnoreCase);

            if (!isEnabled)
            {
                continue;
            }

            _moduleUiRegistry.RegisterNavItem(
                descriptor.ModuleId,
                descriptor.Label,
                descriptor.Href,
                descriptor.Icon,
                descriptor.SortOrder);

            _moduleUiRegistry.RegisterPage(descriptor.RouteKey, descriptor.ComponentType);
        }

        _lastInstalledModuleStatuses = installedModuleStatuses;
        _logger.LogInformation("Refreshed module UI registrations from installed module statuses.");
    }

    private static bool AreStatusesEqual(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var (moduleId, status) in left)
        {
            if (!right.TryGetValue(moduleId, out var otherStatus))
            {
                return false;
            }

            if (!string.Equals(status, otherStatus, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private sealed record ModuleUiDescriptor(
        string ModuleId,
        string Label,
        string Href,
        string Icon,
        int SortOrder,
        string RouteKey,
        Type ComponentType);
}
