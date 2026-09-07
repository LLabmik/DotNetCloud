using DotNetCloud.Core.Data.Context;
using DotNetCloud.UI.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Server.Initialization;

/// <summary>
/// Registers Home-page widget components for enabled modules. Read-only: it never seeds
/// <c>InstalledModules</c> (that is <see cref="ModuleUiRegistrationHostedService"/>'s job on
/// first run); it polls every 15 seconds so an initially empty read self-heals on the next tick.
/// </summary>
internal sealed class WidgetUiRegistrationHostedService : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(15);

    private static readonly WidgetDescriptor[] KnownWidgetDescriptors =
    [
        new("dotnetcloud.files", "Files", "📁", "/apps/files", typeof(DotNetCloud.Modules.Files.Widget.FilesWidget), 10),
        new("dotnetcloud.chat", "Chat", "💬", "/apps/chat", typeof(DotNetCloud.Modules.Chat.Widget.ChatWidget), 20),
        new("dotnetcloud.contacts", "Contacts", "👤", "/apps/contacts", typeof(DotNetCloud.Modules.Contacts.Widget.ContactsWidget), 30),
        new("dotnetcloud.calendar", "Calendar", "📅", "/apps/calendar", typeof(DotNetCloud.Modules.Calendar.Widget.CalendarWidget), 40),
        new("dotnetcloud.notes", "Notes", "📝", "/apps/notes", typeof(DotNetCloud.Modules.Notes.Widget.NotesWidget), 50),
        new("dotnetcloud.tracks", "Tracks", "📊", "/apps/tracks", typeof(DotNetCloud.Modules.Tracks.Widget.TracksWidget), 60),
        new("dotnetcloud.photos", "Photos", "🖼️", "/apps/photos", typeof(DotNetCloud.Modules.Photos.Widget.PhotosWidget), 70),
        new("dotnetcloud.music", "Music", "🎵", "/apps/music", typeof(DotNetCloud.Modules.Music.Widget.MusicWidget), 80),
        new("dotnetcloud.video", "Video", "🎬", "/apps/video", typeof(DotNetCloud.Modules.Video.Widget.VideoWidget), 90),
        new("dotnetcloud.ai", "AI Assistant", "🤖", "/apps/ai", typeof(DotNetCloud.Modules.AI.Widget.AiWidget), 100),
        new("dotnetcloud.bookmarks", "Bookmarks", "🔖", "/apps/bookmarks", typeof(DotNetCloud.Modules.Bookmarks.Widget.BookmarksWidget), 110),
        new("dotnetcloud.email", "Email", "✉️", "/apps/email", typeof(DotNetCloud.Modules.Email.Widget.EmailWidget), 120),
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WidgetUiRegistry _widgetUiRegistry;
    private readonly ILogger<WidgetUiRegistrationHostedService> _logger;
    private IReadOnlyDictionary<string, string> _lastInstalledModuleStatuses =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="WidgetUiRegistrationHostedService"/> class.
    /// </summary>
    public WidgetUiRegistrationHostedService(
        IServiceScopeFactory scopeFactory,
        WidgetUiRegistry widgetUiRegistry,
        ILogger<WidgetUiRegistrationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
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
            await RefreshAsync(stoppingToken);
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RefreshCoreAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down — propagate so the loop exits cleanly.
            throw;
        }
        catch (Exception ex)
        {
            // DB outage or transient error — log and retry on the next tick. Never let a
            // background-service exception take down the host during a database outage.
            _logger.LogError(ex, "Widget UI registration refresh failed; will retry on the next cycle.");
        }
    }

    private async Task RefreshCoreAsync(CancellationToken cancellationToken)
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

        foreach (var descriptor in KnownWidgetDescriptors)
        {
            var isEnabled = installedModuleStatuses.TryGetValue(descriptor.ModuleId, out var status)
                && string.Equals(status, "Enabled", StringComparison.OrdinalIgnoreCase);

            if (isEnabled)
            {
                _widgetUiRegistry.RegisterWidget(
                    descriptor.ModuleId,
                    descriptor.Title,
                    descriptor.Icon,
                    descriptor.Href,
                    descriptor.ComponentType,
                    descriptor.SortOrder);
            }
            else
            {
                _widgetUiRegistry.UnregisterModule(descriptor.ModuleId);
            }
        }

        _lastInstalledModuleStatuses = installedModuleStatuses;
        _logger.LogInformation("Refreshed widget UI registrations from installed module statuses.");
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
            if (!right.TryGetValue(moduleId, out var otherStatus)
                || !string.Equals(status, otherStatus, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Describes a widget registered for an enabled module.
    /// </summary>
    private sealed record WidgetDescriptor(
        string ModuleId,
        string Title,
        string Icon,
        string Href,
        Type ComponentType,
        int SortOrder);
}
