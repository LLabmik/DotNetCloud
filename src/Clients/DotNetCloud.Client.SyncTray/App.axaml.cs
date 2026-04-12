using System.Runtime.InteropServices;
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
    private int _shutdownRequested;
    private int _cleanupDone;

    // Must be kept alive for the lifetime of the process — GC kills the handlers otherwise.
    private readonly List<PosixSignalRegistration> _signalRegistrations = [];

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

            // Handle POSIX signals so the process shuts down cleanly
            // when the desktop session ends (logout / shutdown).
            RegisterPosixShutdownSignals(desktop);
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

    private void RegisterPosixShutdownSignals(IClassicDesktopStyleApplicationLifetime desktop)
    {
        void HandleSignal(PosixSignalContext ctx)
        {
            ctx.Cancel = true; // Prevent immediate termination; we shut down gracefully.
            if (Interlocked.CompareExchange(ref _shutdownRequested, 1, 0) == 0)
            {
                // Fast-path shutdown: cancel background tasks, flush logs, exit.
                // Avoid Avalonia UI-thread work (tray icon, windows) — the
                // session is tearing down and the display may already be gone.
                _ = Task.Run(() =>
                {
                    try
                    { _cts?.Cancel(); }
                    catch { }
                    try
                    { _syncManager?.StopAllAsync(CancellationToken.None).Wait(TimeSpan.FromSeconds(3)); }
                    catch { }
                    Log.CloseAndFlush();
                    Environment.Exit(0);
                });
            }
        }

        _signalRegistrations.Add(PosixSignalRegistration.Create(PosixSignal.SIGTERM, HandleSignal));
        _signalRegistrations.Add(PosixSignalRegistration.Create(PosixSignal.SIGINT, HandleSignal));
        _signalRegistrations.Add(PosixSignalRegistration.Create(PosixSignal.SIGHUP, HandleSignal));
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        // Run heavy cleanup on a background thread so the XSMP session
        // management response can return to the session manager immediately.
        // Without this, RunShutdownCleanup blocks the UI thread and the
        // session manager times out → "unknown program blocking logout".
        _ = Task.Run(() =>
        {
            RunShutdownCleanup();
            Environment.Exit(0);
        });
    }

    private void RunShutdownCleanup()
    {
        // Guard against being called twice (OnExit + fallback timer).
        if (Interlocked.CompareExchange(ref _cleanupDone, 1, 0) != 0)
            return;

        try
        { _cts?.Cancel(); }
        catch { /* best-effort */ }

        try
        { _trayIconManager?.Dispose(); }
        catch { /* best-effort */ }

        // Stop all sync engines gracefully.
        if (_syncManager is not null)
        {
            try
            { _syncManager.StopAllAsync().GetAwaiter().GetResult(); }
            catch { /* best-effort */ }
        }

        if (_services is IAsyncDisposable asyncServices)
        {
            try
            { asyncServices.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            catch { /* best-effort */ }
        }
        else
        {
            try
            { (_services as IDisposable)?.Dispose(); }
            catch { /* best-effort */ }
        }

        foreach (var reg in _signalRegistrations)
        {
            try
            { reg.Dispose(); }
            catch { /* best-effort */ }
        }

        Log.CloseAndFlush();
    }
}
