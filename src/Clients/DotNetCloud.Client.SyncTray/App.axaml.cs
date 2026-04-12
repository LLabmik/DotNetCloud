using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using DotNetCloud.Client.Core;
using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.Auth;
using DotNetCloud.Client.Core.SelectiveSync;
using DotNetCloud.Client.Core.Sync;
using DotNetCloud.Client.Core.SyncIgnore;
using DotNetCloud.Client.SyncTray.Notifications;
using DotNetCloud.Client.SyncTray.Startup;
using DotNetCloud.Client.SyncTray.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace DotNetCloud.Client.SyncTray;

/// <summary>
/// Root Avalonia Application.  Initialises DI, starts the sync context manager,
/// and hosts the system-tray icon for the lifetime of the process.
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _services;
    private TrayIconManager? _trayIconManager;
    private ISyncContextManager? _syncManager;
    private CancellationTokenSource? _cts;

    /// <inheritdoc/>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc/>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _services = BuildServices();
            _cts = new CancellationTokenSource();

            var logger = _services.GetRequiredService<ILogger<App>>();
            logger.LogInformation("DotNetCloud SyncTray starting...");

            var startupManager = _services.GetRequiredService<IDesktopStartupManager>();
            startupManager.TryEnsureApplicationLauncher();

            _syncManager = _services.GetRequiredService<ISyncContextManager>();
            _trayIconManager = _services.GetRequiredService<TrayIconManager>();

            _trayIconManager.Initialize();
            logger.LogInformation("Tray icon manager initialized");

            // Load persisted sync contexts and start engines.
            _ = StartSyncManagerAsync(logger, _cts.Token);

            desktop.Exit += OnExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    // ── DI composition ────────────────────────────────────────────────────

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // Configure Serilog for console + file logging.
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DotNetCloud", "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("System.Net.Http.HttpClient", Serilog.Events.LogEventLevel.Warning)
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(logDir, "sync-tray.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        services.AddLogging(b => b.AddSerilog(Log.Logger));

        // HTTP client factory for OAuth2 flows.
        services.AddHttpClient();
        services.AddHttpClient<IOAuth2Service, OAuth2Service>()
            .ConfigurePrimaryHttpMessageHandler(OAuthHttpClientHandlerFactory.CreateHandler);

        // Core view-models.
        services.AddSingleton<TrayViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // Sync ignore parser (used by settings UI).
        services.AddTransient<ISyncIgnoreParser, SyncIgnoreParser>();

        // Selective sync configuration.
        services.AddSingleton<ISelectiveSyncConfig, SelectiveSyncConfig>();

        // Sync context manager (replaces the former SyncService process).
        services.AddSyncContextManager();

        // Startup integration for Linux XDG autostart.
        services.AddSingleton<IDesktopStartupManager, DesktopStartupManager>();

        // Chat real-time client used by tray unread badge features.
        services.AddSingleton<IChatSignalRClient, NoOpChatSignalRClient>();

        // Chat API client used by the quick-reply window.
        services.AddSingleton<IChatApiClient, NoOpChatApiClient>();

        // Tray icon manager.
        services.AddSingleton<TrayIconManager>();

        // Platform-specific notification service.
        services.AddSingleton<INotificationService>(static sp =>
            NotificationServiceFactory.Create(sp.GetRequiredService<ILogger<INotificationService>>()));

        return services.BuildServiceProvider();
    }

    private async Task StartSyncManagerAsync(Microsoft.Extensions.Logging.ILogger logger, CancellationToken cancellationToken)
    {
        if (_syncManager is null || _services is null)
            return;

        try
        {
            await _syncManager.LoadContextsAsync(cancellationToken);
            logger.LogInformation("Sync context manager loaded.");

            var trayVm = _services.GetRequiredService<TrayViewModel>();
            await trayVm.RefreshAccountsAsync();

            // First-run onboarding: prompt for account when none exist.
            var contexts = await _syncManager.GetContextsAsync();
            if (contexts.Count == 0)
            {
                logger.LogInformation("No sync accounts configured. Launching first-run add-account flow.");
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var vm = _services.GetRequiredService<SettingsViewModel>();
                    await vm.BeginAddAccountFlowAsync();
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start sync context manager.");
        }
    }

    // ── Shutdown ──────────────────────────────────────────────────────────

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _cts?.Cancel();
        _trayIconManager?.Dispose();

        // Stop all sync engines gracefully.
        if (_syncManager is not null)
        {
            _syncManager.StopAllAsync().GetAwaiter().GetResult();
        }

        if (_services is IAsyncDisposable asyncServices)
        {
            asyncServices.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        else
        {
            (_services as IDisposable)?.Dispose();
        }
        Log.CloseAndFlush();
    }
}
