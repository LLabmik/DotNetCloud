using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DotNetCloud.Client.SyncTray.ViewModels;
using DotNetCloud.Client.SyncTray.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.SyncTray;

/// <summary>
/// Manages the system-tray icon, context menu, and tray interactions for the
/// lifetime of the application.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private readonly TrayViewModel _trayVm;
    private readonly IServiceProvider _services;
    private readonly ILogger<TrayIconManager> _logger;

    private TrayIcon? _trayIcon;
    private SettingsWindow? _settingsWindow;

    // Menu items that need dynamic updates.
    private NativeMenuItem? _statusItem;
    private NativeMenuItem? _syncNowItem;
    private NativeMenuItem? _pauseResumeItem;

    /// <summary>Initializes a new <see cref="TrayIconManager"/>.</summary>
    public TrayIconManager(TrayViewModel trayVm, IServiceProvider services, ILogger<TrayIconManager> logger)
    {
        _trayVm = trayVm;
        _services = services;
        _logger = logger;
    }

    // ── Initialization ────────────────────────────────────────────────────

    /// <summary>Creates the tray icon and subscribes to view-model changes.</summary>
    public void Initialize()
    {
        _trayIcon = new TrayIcon();

        var menu = BuildMenu();
        _trayIcon.Menu = menu;
        _trayIcon.Icon = CreateStatusIcon(TrayState.Offline);
        _trayIcon.ToolTipText = _trayVm.Tooltip;
        _trayIcon.IsVisible = true;

        // Ensure menu shows on click (Avalonia TrayIcon menu behavior varies by platform)
        _trayIcon.Clicked += (_, _) =>
        {
            // Menu should show automatically, but log the click for diagnostics
            _logger.LogDebug("Tray icon clicked. Menu items: {Count}", menu.Items.Count);
        };

        _trayVm.PropertyChanged += OnTrayViewModelChanged;

        _logger.LogInformation("Tray icon initialized with {MenuCount} menu items. State: {State}",
            menu.Items.Count, _trayVm.OverallState);
    }

    // ── Menu construction ─────────────────────────────────────────────────

    private NativeMenu BuildMenu()
    {
        var menu = new NativeMenu();

        // Status summary (read-only header)
        _statusItem = new NativeMenuItem(_trayVm.Tooltip) { IsEnabled = false };
        menu.Items.Add(_statusItem);
        menu.Items.Add(new NativeMenuItemSeparator());

        // Sync now
        _syncNowItem = new NativeMenuItem("Sync now");
        _syncNowItem.Click += (_, _) => _ = _trayVm.SyncNowAllAsync();
        menu.Items.Add(_syncNowItem);

        // Pause / Resume
        _pauseResumeItem = new NativeMenuItem("Pause syncing");
        _pauseResumeItem.Click += OnPauseResumeClicked;
        menu.Items.Add(_pauseResumeItem);

        menu.Items.Add(new NativeMenuItemSeparator());

        // Open sync folder
        var openFolderItem = new NativeMenuItem("Open sync folder");
        openFolderItem.Click += OnOpenSyncFolderClicked;
        menu.Items.Add(openFolderItem);

        // Open in browser
        var openBrowserItem = new NativeMenuItem("Open DotNetCloud in browser");
        openBrowserItem.Click += OnOpenBrowserClicked;
        menu.Items.Add(openBrowserItem);

        menu.Items.Add(new NativeMenuItemSeparator());

        // Settings
        var settingsItem = new NativeMenuItem("Settings…");
        settingsItem.Click += OnSettingsClicked;
        menu.Items.Add(settingsItem);

        menu.Items.Add(new NativeMenuItemSeparator());

        // Quit
        var quitItem = new NativeMenuItem("Quit");
        quitItem.Click += OnQuitClicked;
        menu.Items.Add(quitItem);

        return menu;
    }

    // ── ViewModel → tray icon sync ────────────────────────────────────────

    private void OnTrayViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (e.PropertyName == nameof(TrayViewModel.OverallState))
            {
                _trayIcon!.Icon = CreateStatusIcon(_trayVm.OverallState);
            }
            else if (e.PropertyName == nameof(TrayViewModel.Tooltip))
            {
                _trayIcon!.ToolTipText = _trayVm.Tooltip;
                if (_statusItem is not null)
                    _statusItem.Header = _trayVm.Tooltip;
            }
            else if (e.PropertyName == nameof(TrayViewModel.IsPaused))
            {
                if (_pauseResumeItem is not null)
                    _pauseResumeItem.Header = _trayVm.IsPaused ? "Resume syncing" : "Pause syncing";
            }
        });
    }

    // ── Menu event handlers ───────────────────────────────────────────────

    private void OnPauseResumeClicked(object? sender, EventArgs e)
    {
        if (_trayVm.IsPaused)
            _ = _trayVm.ResumeAllAsync();
        else
            _ = _trayVm.PauseAllAsync();
    }

    private void OnOpenSyncFolderClicked(object? sender, EventArgs e)
    {
        var firstAccount = _trayVm.Accounts.FirstOrDefault();
        if (firstAccount is null) return;

        OpenFolderInExplorer(firstAccount.LocalFolderPath);
    }

    private void OnOpenBrowserClicked(object? sender, EventArgs e)
    {
        var firstAccount = _trayVm.Accounts.FirstOrDefault();
        if (firstAccount is null) return;

        try
        {
            Process.Start(new ProcessStartInfo(firstAccount.ServerBaseUrl)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open browser for {Url}.", firstAccount.ServerBaseUrl);
        }
    }

    private void OnSettingsClicked(object? sender, EventArgs e)
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        var vm = _services.GetRequiredService<SettingsViewModel>();
        _settingsWindow = new SettingsWindow(vm);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private static void OnQuitClicked(object? sender, EventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            lifetime.Shutdown();
    }

    // ── Icon generation ───────────────────────────────────────────────────

    /// <summary>
    /// Creates a 32×32 solid-colour circle icon for the given tray state.
    /// These are placeholder icons; production icons should be replaced with
    /// proper asset files under <c>Assets/</c>.
    /// </summary>
    private static WindowIcon CreateStatusIcon(TrayState state)
    {
        // Map state → RGB colour.
        var (r, g, b) = state switch
        {
            TrayState.Idle => (0x00, 0xB0, 0x40),     // Green
            TrayState.Syncing => (0x00, 0x78, 0xD4),   // Windows blue
            TrayState.Paused => (0xFF, 0xA5, 0x00),    // Amber
            TrayState.Error => (0xC4, 0x1E, 0x3A),     // Crimson
            _ => (0x70, 0x70, 0x70),                   // Grey (Offline)
        };

        return new WindowIcon(CreateCircleBitmap(32, r, g, b));
    }

    private static Bitmap CreateCircleBitmap(int size, int r, int g, int b)
    {
        var bmp = new WriteableBitmap(
            new PixelSize(size, size),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using var fb = bmp.Lock();

        var pixels = new byte[size * size * 4];
        float centre = (size - 1) / 2f;
        float radius = centre - 1f;

        for (int py = 0; py < size; py++)
        {
            for (int px = 0; px < size; px++)
            {
                float dx = px - centre;
                float dy = py - centre;
                float dist = MathF.Sqrt(dx * dx + dy * dy);

                int idx = (py * size + px) * 4;

                if (dist <= radius)
                {
                    // Inside circle — opaque colour (BGRA order).
                    pixels[idx + 0] = (byte)b;
                    pixels[idx + 1] = (byte)g;
                    pixels[idx + 2] = (byte)r;
                    pixels[idx + 3] = 255;
                }
                else if (dist <= radius + 1.5f)
                {
                    // Anti-aliased edge — alpha blend.
                    float alpha = radius + 1.5f - dist;
                    byte a = (byte)(alpha * 255f);
                    pixels[idx + 0] = (byte)(b * alpha);
                    pixels[idx + 1] = (byte)(g * alpha);
                    pixels[idx + 2] = (byte)(r * alpha);
                    pixels[idx + 3] = a;
                }
                // else transparent (all zeros)
            }
        }

        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, fb.Address, pixels.Length);
        return bmp;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void OpenFolderInExplorer(string path)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer.exe", $"\"{path}\"");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start(new ProcessStartInfo("xdg-open", $"\"{path}\"")
                {
                    UseShellExecute = true,
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
        }
        catch (Exception)
        {
            // Folder open failure is non-fatal.
        }
    }

    // ── Disposal ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Dispose()
    {
        _trayVm.PropertyChanged -= OnTrayViewModelChanged;
        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}
