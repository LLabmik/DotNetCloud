using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DotNetCloud.Client.Core.Auth;
using DotNetCloud.Client.SyncTray.Ipc;
using DotNetCloud.Client.SyncTray.Notifications;
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

            _ipcClient = _services.GetRequiredService<IIpcClient>();
            _trayIconManager = _services.GetRequiredService<TrayIconManager>();

            _trayIconManager.Initialize();
            logger.LogInformation("Tray icon manager initialized");

            // Connect to the background SyncService (reconnects automatically on failure).
            _ = _ipcClient.ConnectAsync(_cts.Token);
            logger.LogInformation("IPC client connection started");

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
        services.AddHttpClient<OAuth2Service>();
        services.AddTransient<IOAuth2Service, OAuth2Service>();

        // Core view-models.
        services.AddSingleton<TrayViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // IPC client for communication with SyncService.
        services.AddSingleton<IIpcClient, IpcClient>();

        // Tray icon manager.
        services.AddSingleton<TrayIconManager>();

        // Platform-specific notification service.
        services.AddSingleton<INotificationService>(static sp =>
            NotificationServiceFactory.Create(sp.GetRequiredService<ILogger<INotificationService>>()));

        return services.BuildServiceProvider();
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
        Log.CloseAndFlush();
    }
}
