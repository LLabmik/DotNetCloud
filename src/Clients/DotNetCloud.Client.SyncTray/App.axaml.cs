using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using DotNetCloud.Client.Core;
using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.Auth;
using DotNetCloud.Client.Core.SelectiveSync;
using DotNetCloud.Client.Core.SyncIgnore;
using DotNetCloud.Client.SyncService.Ipc;
using DotNetCloud.Client.SyncTray.Ipc;
using DotNetCloud.Client.SyncTray.Notifications;
using DotNetCloud.Client.SyncTray.Startup;
using DotNetCloud.Client.SyncTray.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace DotNetCloud.Client.SyncTray;

/// <summary>
/// Root Avalonia Application.  Initialises DI, connects the IPC client to
/// the background SyncService, and hosts the system-tray icon for the lifetime
/// of the process.
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _services;
    private TrayIconManager? _trayIconManager;
    private IIpcClient? _ipcClient;
    private CancellationTokenSource? _cts;
    private readonly SemaphoreSlim _serviceRecoveryLock = new(1, 1);
    private DateTimeOffset _lastServiceRecoveryAttemptUtc = DateTimeOffset.MinValue;

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

            _ipcClient = _services.GetRequiredService<IIpcClient>();
            _trayIconManager = _services.GetRequiredService<TrayIconManager>();

            _ipcClient.ConnectionStateChanged += (_, connected) =>
            {
                if (!connected && _cts is not null)
                {
                    _ = TryRecoverServiceAsync(startupManager, _cts.Token);
                }
            };

            _trayIconManager.Initialize();
            logger.LogInformation("Tray icon manager initialized");

            // Connect to the background SyncService (reconnects automatically on failure).
            _ = StartIpcClientAsync(startupManager, _cts.Token);
            logger.LogInformation("IPC client connection started");

            // First-run onboarding: prompt for server/account when none exist.
            _ = PromptForInitialAccountIfNeededAsync(_cts.Token);

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

        // IPC client for communication with SyncService.
        services.AddSingleton<IIpcClient, IpcClient>();

        // Startup integration for service bootstrap and Linux XDG autostart.
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

    private async Task StartIpcClientAsync(IDesktopStartupManager startupManager, CancellationToken cancellationToken)
    {
        if (_ipcClient is null || _services is null)
            return;

        var logger = _services.GetRequiredService<ILogger<App>>();
        var startedService = startupManager.TryEnsureSyncServiceStarted();

        if (OperatingSystem.IsLinux() && startedService)
        {
            await WaitForLinuxIpcSocketAsync(logger, cancellationToken);
        }

        await _ipcClient.ConnectAsync(cancellationToken);
    }

    private async Task TryRecoverServiceAsync(IDesktopStartupManager startupManager, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux() || _services is null)
        {
            return;
        }

        var logger = _services.GetRequiredService<ILogger<App>>();

        await _serviceRecoveryLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            if ((now - _lastServiceRecoveryAttemptUtc) < TimeSpan.FromSeconds(10))
            {
                return;
            }

            _lastServiceRecoveryAttemptUtc = now;
            var started = startupManager.TryEnsureSyncServiceStarted();
            if (started)
            {
                logger.LogInformation("IPC disconnected; attempted SyncService recovery.");
                await WaitForLinuxIpcSocketAsync(logger, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed while attempting SyncService recovery after IPC disconnect.");
        }
        finally
        {
            _serviceRecoveryLock.Release();
        }
    }

    private static async Task WaitForLinuxIpcSocketAsync(Microsoft.Extensions.Logging.ILogger logger, CancellationToken cancellationToken)
    {
        var socketPath = IpcServer.UnixSocketPath;
        if (File.Exists(socketPath))
            return;

        const int maxAttempts = 100;
        for (var i = 0; i < maxAttempts && !cancellationToken.IsCancellationRequested; i++)
        {
            if (File.Exists(socketPath))
            {
                logger.LogDebug("SyncService IPC socket ready at {Path}.", socketPath);
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        logger.LogDebug("SyncService IPC socket not ready after startup wait window ({Path}).", socketPath);
    }

    private async Task PromptForInitialAccountIfNeededAsync(CancellationToken cancellationToken)
    {
        if (_ipcClient is null || _services is null)
            return;

        var logger = _services.GetRequiredService<ILogger<App>>();

        // Give IPC connection a short window to come online.
        for (var i = 0; i < 10 && !_ipcClient.IsConnected && !cancellationToken.IsCancellationRequested; i++)
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

        if (!_ipcClient.IsConnected || cancellationToken.IsCancellationRequested)
            return;

        try
        {
            var contexts = await _ipcClient.ListContextsAsync(cancellationToken);
            if (contexts.Count > 0)
                return;

            logger.LogInformation("No sync accounts configured. Launching first-run add-account flow.");

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var vm = _services.GetRequiredService<SettingsViewModel>();
                await vm.BeginAddAccountFlowAsync();
            });
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to launch first-run add-account flow.");
        }
    }

    // ── Shutdown ──────────────────────────────────────────────────────────

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _cts?.Cancel();
        _trayIconManager?.Dispose();

        if (_ipcClient is IAsyncDisposable asyncDisposable)
        {
            asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        else if (_ipcClient is IDisposable disposable)
        {
            disposable.Dispose();
        }

        (_services as IDisposable)?.Dispose();
        _serviceRecoveryLock.Dispose();
        Log.CloseAndFlush();
    }
}
