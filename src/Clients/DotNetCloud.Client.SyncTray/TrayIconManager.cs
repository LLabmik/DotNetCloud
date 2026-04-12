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
    internal enum TrayChatBadgeKind
    {
        None,
        Unread,
        Mention,
    }

    private readonly TrayViewModel _trayVm;
    private readonly IServiceProvider _services;
    private readonly ILogger<TrayIconManager> _logger;

    private TrayIcon? _trayIcon;
    private SettingsWindow? _settingsWindow;
    private SyncProgressWindow? _syncProgressWindow;
    private QuickReplyWindow? _quickReplyWindow;

    // Menu items that need dynamic updates.
    private NativeMenuItem? _statusItem;
    private NativeMenuItem? _conflictsItem;
    private NativeMenuItem? _errorItem;
    private NativeMenuItem? _syncNowItem;
    private NativeMenuItem? _pauseResumeItem;
    private NativeMenuItem? _quickReplyItem;

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
        _trayIcon.Icon = CreateStatusIcon(TrayState.Offline, _trayVm.ChatUnreadCount, _trayVm.ChatHasMentions);
        _trayIcon.ToolTipText = _trayVm.Tooltip;
        _trayIcon.IsVisible = true;

        // Ensure menu shows on click (Avalonia TrayIcon menu behavior varies by platform)
        _trayIcon.Clicked += (_, _) =>
        {
            _logger.LogDebug("Tray icon clicked. Menu items: {Count}", menu.Items.Count);
            _ = _trayVm.RefreshAccountsAsync();
            Dispatcher.UIThread.Post(OpenSyncProgressWindow);
        };

        _trayVm.PropertyChanged += OnTrayViewModelChanged;
        _trayVm.OpenQuickReplyRequested += OnOpenQuickReplyRequested;

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

        // Error details item — enabled only when one or more accounts have errors
        _errorItem = new NativeMenuItem("View sync error…") { IsEnabled = false };
        _errorItem.Click += (_, _) => OnErrorDetailsClicked();
        menu.Items.Add(_errorItem);

        // Conflicts item — enabled only when there are unresolved conflicts
        _conflictsItem = new NativeMenuItem("Conflicts (0)") { IsEnabled = false };
        _conflictsItem.Click += (_, _) => OnConflictsClicked();
        menu.Items.Add(_conflictsItem);

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

        // Open sync logs
        var openLogsItem = new NativeMenuItem("Open Sync Logs");
        openLogsItem.Click += OnOpenLogsClicked;
        menu.Items.Add(openLogsItem);

        // Open in browser
        var openBrowserItem = new NativeMenuItem("Open DotNetCloud in browser");
        openBrowserItem.Click += OnOpenBrowserClicked;
        menu.Items.Add(openBrowserItem);

        // Quick reply to chat (enabled when there are unread messages)
        _quickReplyItem = new NativeMenuItem("Reply to Chat…") { IsEnabled = false };
        _quickReplyItem.Click += OnQuickReplyClicked;
        menu.Items.Add(_quickReplyItem);

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
                _trayIcon!.Icon = CreateStatusIcon(_trayVm.OverallState, _trayVm.ChatUnreadCount, _trayVm.ChatHasMentions);

                // Show/hide error details menu item based on error state.
                if (_errorItem is not null)
                {
                    var hasError = _trayVm.OverallState == TrayState.Error;
                    _errorItem.IsEnabled = hasError;
                    _errorItem.Header = hasError
                        ? "View sync error…"
                        : "No sync errors";
                }
            }
            else if (e.PropertyName is nameof(TrayViewModel.ChatUnreadCount) or nameof(TrayViewModel.ChatHasMentions))
            {
                _trayIcon!.Icon = CreateStatusIcon(_trayVm.OverallState, _trayVm.ChatUnreadCount, _trayVm.ChatHasMentions);
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
            else if (e.PropertyName is nameof(TrayViewModel.ConflictCount) or nameof(TrayViewModel.HasConflicts))
            {
                if (_conflictsItem is not null)
                {
                    _conflictsItem.Header = _trayVm.ConflictCount == 0
                        ? "Conflicts (0)"
                        : $"View conflicts ({_trayVm.ConflictCount})…";
                    _conflictsItem.IsEnabled = _trayVm.HasConflicts;
                }
            }
            else if (e.PropertyName is nameof(TrayViewModel.ChatUnreadCount) or nameof(TrayViewModel.ChatHasMentions))
            {
                if (_quickReplyItem is not null)
                    _quickReplyItem.IsEnabled = _trayVm.ChatUnreadCount > 0;
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
        if (firstAccount is null)
            return;

        OpenFolderInExplorer(firstAccount.LocalFolderPath);
    }

    private void OnOpenLogsClicked(object? sender, EventArgs e)
    {
        try
        {
            var logDirectory = GetLogDirectory();
            OpenFolderInExplorer(logDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open sync logs folder from tray menu.");
        }
    }

    private void OnOpenBrowserClicked(object? sender, EventArgs e)
    {
        var firstAccount = _trayVm.Accounts.FirstOrDefault();
        if (firstAccount is null)
            return;

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

    private void OnQuickReplyClicked(object? sender, EventArgs e)
    {
        var channelId = _trayVm.GetMostRecentChannelId();
        if (channelId is null)
            return;

        var serverBaseUrl = _trayVm.Accounts.FirstOrDefault()?.ServerBaseUrl;
        if (string.IsNullOrWhiteSpace(serverBaseUrl))
            return;

        // GetMostRecentChannelId picks from _chatUnreadByChannel; ask the VM for the display name.
        var channelName = _trayVm.GetChannelDisplayName(channelId);
        OpenQuickReplyWindow(channelId, channelName, serverBaseUrl);
    }

    private void OnOpenQuickReplyRequested(string channelId, string channelName, string serverBaseUrl)
    {
        Dispatcher.UIThread.Post(() => OpenQuickReplyWindow(channelId, channelName, serverBaseUrl));
    }

    private void OpenQuickReplyWindow(string channelId, string channelName, string serverBaseUrl)
    {
        if (_quickReplyWindow is not null)
        {
            _quickReplyWindow.Activate();
            return;
        }

        var vm = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<QuickReplyViewModel>(
            _services,
            channelId,
            channelName,
            serverBaseUrl);

        _quickReplyWindow = new QuickReplyWindow(vm);
        _quickReplyWindow.Closed += (_, _) => _quickReplyWindow = null;
        _quickReplyWindow.Show();
    }

    private void OnSettingsClicked(object? sender, EventArgs e)
    {
        OpenSettingsWindow();
    }

    private void OnErrorDetailsClicked()
    {
        var summary = _trayVm.GetErrorSummary();
        _logger.LogInformation("Sync error details: {Summary}", summary ?? "(no details)");

        // Open Settings to the Accounts tab so user can see which account(s) have errors.
        OpenSettingsWindow();
    }

    private void OnConflictsClicked()
    {
        OpenSettingsWindow(conflictsTab: true);
    }

    private void OpenSettingsWindow(bool conflictsTab = false)
    {
        if (_settingsWindow is not null)
        {
            if (conflictsTab)
            {
                if (_settingsWindow.DataContext is SettingsViewModel vm)
                {
                    vm.SelectedSettingsTab = 4; // Conflicts tab
                    vm.SelectedConflictsTab = 0;
                }
            }
            _settingsWindow.Activate();
            return;
        }

        var settingsVm = _services.GetRequiredService<SettingsViewModel>();
        if (conflictsTab)
        {
            settingsVm.SelectedSettingsTab = 4; // Conflicts tab
            settingsVm.SelectedConflictsTab = 0;
            _ = settingsVm.RefreshConflictsAsync();
        }
        _settingsWindow = new SettingsWindow(settingsVm);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private void OpenSyncProgressWindow()
    {
        if (_syncProgressWindow is not null)
        {
            _syncProgressWindow.Activate();
            return;
        }

        var vm = new SyncProgressViewModel(_trayVm);
        _syncProgressWindow = new SyncProgressWindow(vm);
        _syncProgressWindow.Closed += (_, _) =>
        {
            vm.Dispose();
            _syncProgressWindow = null;
        };
        _syncProgressWindow.Show();
    }

    private void OnQuitClicked(object? sender, EventArgs e)
    {
        _logger.LogInformation("Quit clicked from tray menu.");

        try
        {
            if (_trayIcon is not null)
            {
                _trayIcon.IsVisible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispose tray icon during quit.");
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            {
                lifetime.Shutdown();
            }
        });

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            Environment.Exit(0);
        });
    }

    // ── Icon generation ───────────────────────────────────────────────────

    /// <summary>
    /// Creates a 32×32 solid-colour circle icon for the given tray state.
    /// These are placeholder icons; production icons should be replaced with
    /// proper asset files under <c>Assets/</c>.
    /// </summary>
    private static WindowIcon CreateStatusIcon(TrayState state, int chatUnreadCount, bool chatHasMentions)
    {
        // Map state → RGB colour.
        var (r, g, b) = state switch
        {
            TrayState.Idle => (0x00, 0xB0, 0x40),     // Green
            TrayState.Syncing => (0x00, 0x78, 0xD4),   // Windows blue
            TrayState.Paused => (0x66, 0x33, 0x99),    // RebeccaPurple
            TrayState.Error => (0xC4, 0x1E, 0x3A),     // Crimson
            TrayState.Conflict => (0xFF, 0x8C, 0x00),  // Dark orange
            _ => (0x70, 0x70, 0x70),                   // Grey (Offline)
        };

        var badgeKind = GetChatBadgeKind(chatUnreadCount, chatHasMentions);

        return new WindowIcon(CreateCircleBitmap(32, r, g, b, state, badgeKind));
    }

    internal static TrayChatBadgeKind GetChatBadgeKind(int chatUnreadCount, bool chatHasMentions)
    {
        if (chatHasMentions)
            return TrayChatBadgeKind.Mention;

        if (chatUnreadCount > 0)
            return TrayChatBadgeKind.Unread;

        return TrayChatBadgeKind.None;
    }

    internal static Bitmap CreateCircleBitmap(int size, int r, int g, int b, TrayState state, TrayChatBadgeKind badgeKind)
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

        // Draw status symbol overlay on top of circle, before badge.
        DrawStatusSymbol(pixels, size, centre, radius, state);

        if (badgeKind is not TrayChatBadgeKind.None)
        {
            var (badgeR, badgeG, badgeB) = badgeKind == TrayChatBadgeKind.Mention
                ? (0xE6, 0x39, 0x46) // High-priority red badge.
                : (0xFF, 0xB7, 0x03); // Regular unread amber badge.

            var badgeRadius = Math.Max(3, size / 6f);
            var badgeCentreX = size - 8f;
            var badgeCentreY = 8f;

            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float dx = px - badgeCentreX;
                    float dy = py - badgeCentreY;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);

                    if (dist > badgeRadius + 1.2f)
                        continue;

                    var idx = (py * size + px) * 4;
                    var alpha = dist <= badgeRadius ? 1f : badgeRadius + 1.2f - dist;
                    var invAlpha = 1f - alpha;

                    pixels[idx + 0] = (byte)(pixels[idx + 0] * invAlpha + badgeB * alpha);
                    pixels[idx + 1] = (byte)(pixels[idx + 1] * invAlpha + badgeG * alpha);
                    pixels[idx + 2] = (byte)(pixels[idx + 2] * invAlpha + badgeR * alpha);
                    pixels[idx + 3] = 255;
                }
            }
        }

        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, fb.Address, pixels.Length);
        return bmp;
    }

    // ── Symbol drawing ────────────────────────────────────────────────────

    /// <summary>Dispatches to the appropriate symbol drawing method for the given tray state.</summary>
    internal static void DrawStatusSymbol(byte[] pixels, int size, float centre, float radius, TrayState state)
    {
        switch (state)
        {
            case TrayState.Idle:
                DrawCheckmark(pixels, size, centre, radius);
                break;
            case TrayState.Syncing:
                DrawSyncArrows(pixels, size, centre, radius);
                break;
            case TrayState.Paused:
                DrawPauseBars(pixels, size, centre, radius);
                break;
            case TrayState.Error:
                DrawXMark(pixels, size, centre, radius);
                break;
            case TrayState.Conflict:
                DrawExclamation(pixels, size, centre, radius);
                break;
            case TrayState.Offline:
                DrawDash(pixels, size, centre, radius);
                break;
        }
    }

    /// <summary>Draws a ✓ checkmark symbol (Idle state).</summary>
    private static void DrawCheckmark(byte[] pixels, int size, float centre, float radius)
    {
        // Short leg: lower-left to vertex, Long leg: vertex to upper-right.
        DrawAntiAliasedLine(pixels, size, 8f, 16f, 12f, 20f, 2.5f, centre, radius);
        DrawAntiAliasedLine(pixels, size, 12f, 20f, 22f, 10f, 2.5f, centre, radius);
    }

    /// <summary>Draws ⟳ sync arrows symbol (Syncing state).</summary>
    private static void DrawSyncArrows(byte[] pixels, int size, float centre, float radius)
    {
        const float stroke = 2f;

        // Top arrow pointing right.
        DrawAntiAliasedLine(pixels, size, 10f, 12f, 21f, 12f, stroke, centre, radius);
        DrawAntiAliasedLine(pixels, size, 21f, 12f, 18f, 9f, stroke, centre, radius);
        DrawAntiAliasedLine(pixels, size, 21f, 12f, 18f, 15f, stroke, centre, radius);

        // Bottom arrow pointing left.
        DrawAntiAliasedLine(pixels, size, 21f, 19f, 10f, 19f, stroke, centre, radius);
        DrawAntiAliasedLine(pixels, size, 10f, 19f, 13f, 16f, stroke, centre, radius);
        DrawAntiAliasedLine(pixels, size, 10f, 19f, 13f, 22f, stroke, centre, radius);
    }

    /// <summary>Draws ⏸ pause bars symbol (Paused state).</summary>
    private static void DrawPauseBars(byte[] pixels, int size, float centre, float radius)
    {
        // Two vertical bars, symmetrically placed around circle centre.
        // centre = 15.5 → left bar at x=12, right bar at x=19 (3.5px offset each).
        const float barWidth = 3.0f;
        DrawAntiAliasedLine(pixels, size, centre - 3.5f, 9f, centre - 3.5f, 22f, barWidth, centre, radius);
        DrawAntiAliasedLine(pixels, size, centre + 3.5f, 9f, centre + 3.5f, 22f, barWidth, centre, radius);
    }

    /// <summary>Draws ✕ X mark symbol (Error state).</summary>
    private static void DrawXMark(byte[] pixels, int size, float centre, float radius)
    {
        DrawAntiAliasedLine(pixels, size, 9f, 9f, 22f, 22f, 2.5f, centre, radius);
        DrawAntiAliasedLine(pixels, size, 22f, 9f, 9f, 22f, 2.5f, centre, radius);
    }

    /// <summary>Draws ! exclamation mark symbol (Conflict state).</summary>
    private static void DrawExclamation(byte[] pixels, int size, float centre, float radius)
    {
        // Vertical stem.
        DrawAntiAliasedLine(pixels, size, 15.5f, 8f, 15.5f, 18f, 3f, centre, radius);
        // Dot below.
        DrawFilledCircleAt(pixels, size, 15.5f, 22f, 1.8f, centre, radius);
    }

    /// <summary>Draws — horizontal dash symbol (Offline state).</summary>
    private static void DrawDash(byte[] pixels, int size, float centre, float radius)
    {
        DrawAntiAliasedLine(pixels, size, 9f, 15.5f, 22f, 15.5f, 2.5f, centre, radius);
    }

    // ── Pixel drawing helpers ─────────────────────────────────────────────

    /// <summary>
    /// Composites a white pixel with the given alpha onto the pixel buffer,
    /// only if the pixel is within the circle bounds.
    /// </summary>
    private static void SetWhitePixel(byte[] pixels, int size, int px, int py, float alpha, float centre, float radius)
    {
        if ((uint)px >= (uint)size || (uint)py >= (uint)size)
            return;
        if (alpha <= 0f)
            return;

        // Only draw inside circle bounds.
        float dx = px - centre;
        float dy = py - centre;
        if (MathF.Sqrt(dx * dx + dy * dy) > radius)
            return;

        alpha = Math.Clamp(alpha, 0f, 1f);
        int idx = (py * size + px) * 4;
        float inv = 1f - alpha;

        // Premultiplied alpha composite: white (255,255,255) over existing.
        pixels[idx + 0] = (byte)Math.Clamp(255f * alpha + pixels[idx + 0] * inv, 0, 255); // B
        pixels[idx + 1] = (byte)Math.Clamp(255f * alpha + pixels[idx + 1] * inv, 0, 255); // G
        pixels[idx + 2] = (byte)Math.Clamp(255f * alpha + pixels[idx + 2] * inv, 0, 255); // R
        pixels[idx + 3] = (byte)Math.Max(pixels[idx + 3], (byte)(alpha * 255f));           // A
    }

    /// <summary>
    /// Draws an anti-aliased line with the given thickness using a
    /// distance-from-line-segment approach. Composites white over existing pixels.
    /// </summary>
    private static void DrawAntiAliasedLine(
        byte[] pixels, int size,
        float x0, float y0, float x1, float y1,
        float thickness,
        float centre, float radius)
    {
        float halfThick = thickness / 2f;
        float ldx = x1 - x0;
        float ldy = y1 - y0;
        float lenSq = ldx * ldx + ldy * ldy;

        if (lenSq < 0.001f)
        {
            // Degenerate line (point).
            DrawFilledCircleAt(pixels, size, x0, y0, halfThick, centre, radius);
            return;
        }

        int minX = Math.Max(0, (int)MathF.Floor(Math.Min(x0, x1) - halfThick - 1));
        int maxX = Math.Min(size - 1, (int)MathF.Ceiling(Math.Max(x0, x1) + halfThick + 1));
        int minY = Math.Max(0, (int)MathF.Floor(Math.Min(y0, y1) - halfThick - 1));
        int maxY = Math.Min(size - 1, (int)MathF.Ceiling(Math.Max(y0, y1) + halfThick + 1));

        for (int py = minY; py <= maxY; py++)
        {
            for (int px = minX; px <= maxX; px++)
            {
                float t = ((px - x0) * ldx + (py - y0) * ldy) / lenSq;
                t = Math.Clamp(t, 0f, 1f);

                float closestX = x0 + t * ldx;
                float closestY = y0 + t * ldy;
                float ddx = px - closestX;
                float ddy = py - closestY;
                float dist = MathF.Sqrt(ddx * ddx + ddy * ddy);

                if (dist <= halfThick)
                {
                    SetWhitePixel(pixels, size, px, py, 1f, centre, radius);
                }
                else if (dist <= halfThick + 1f)
                {
                    float a = halfThick + 1f - dist;
                    SetWhitePixel(pixels, size, px, py, a, centre, radius);
                }
            }
        }
    }

    /// <summary>Draws a filled anti-aliased circle at the given position (used for exclamation dot).</summary>
    private static void DrawFilledCircleAt(
        byte[] pixels, int size,
        float cx, float cy, float circleRadius,
        float centre, float outerRadius)
    {
        int minX = Math.Max(0, (int)MathF.Floor(cx - circleRadius - 1));
        int maxX = Math.Min(size - 1, (int)MathF.Ceiling(cx + circleRadius + 1));
        int minY = Math.Max(0, (int)MathF.Floor(cy - circleRadius - 1));
        int maxY = Math.Min(size - 1, (int)MathF.Ceiling(cy + circleRadius + 1));

        for (int py = minY; py <= maxY; py++)
        {
            for (int px = minX; px <= maxX; px++)
            {
                float dx = px - cx;
                float dy = py - cy;
                float dist = MathF.Sqrt(dx * dx + dy * dy);

                if (dist <= circleRadius)
                {
                    SetWhitePixel(pixels, size, px, py, 1f, centre, outerRadius);
                }
                else if (dist <= circleRadius + 1f)
                {
                    float a = circleRadius + 1f - dist;
                    SetWhitePixel(pixels, size, px, py, a, centre, outerRadius);
                }
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string GetLogDirectory()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DotNetCloud",
            "logs");

        Directory.CreateDirectory(logDir);
        return logDir;
    }

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
