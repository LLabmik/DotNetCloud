using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.SyncTray.Notifications;

/// <summary>
/// Windows notification service that shows a Shell balloon tooltip via
/// <c>Shell_NotifyIcon</c> through a hidden message-only window.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsNotificationService : INotificationService, IDisposable
{
    // ── Win32 constants ───────────────────────────────────────────────────

    private const int WmUser = 0x0400;
    private const uint NimAdd = 0x00000000;
    private const uint NimModify = 0x00000001;
    private const uint NimDelete = 0x00000002;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint NifInfo = 0x00000010;
    private const uint NiifInfo = 0x00000001;
    private const uint NiifWarning = 0x00000002;
    private const uint NiifError = 0x00000003;
    private const uint NiifNosound = 0x00000010;
    private const int NinBalloonUserClick = WmUser + 5;
    private const int GwlpWndProc = -4;

    // ── P/Invoke ──────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData lpdata);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const string WcStatic = "STATIC";
    private const IntPtr HwndMessage = unchecked((IntPtr)(-3));

    // ── State ─────────────────────────────────────────────────────────────

    private readonly ILogger<INotificationService> _logger;
    private readonly IntPtr _hWnd;
    private readonly WndProcDelegate _windowProc;
    private IntPtr _previousWindowProc;
    private readonly uint _callbackMessage = WmUser + 1;
    private string? _pendingActionUrl;
    private bool _iconAdded;
    private bool _disposed;

    /// <inheritdoc/>
    public Action<string>? OnNotificationActivated { get; set; }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>Initializes the service and creates a message-only window for balloon tips.</summary>
    public WindowsNotificationService(ILogger<INotificationService> logger)
    {
        _logger = logger;
        _windowProc = WindowProc;

        // Create a hidden message-only window to host the Shell notification icon.
        _hWnd = CreateWindowEx(0, WcStatic, "DotNetCloud.SyncTray.Notify",
            0, 0, 0, 0, 0, HwndMessage, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

        if (_hWnd == IntPtr.Zero)
        {
            _logger.LogWarning("Failed to create notification HWND (error {Code}). Notifications will be silent.",
                Marshal.GetLastWin32Error());
            return;
        }

        _previousWindowProc = SetWindowLongPtr(_hWnd, GwlpWndProc, Marshal.GetFunctionPointerForDelegate(_windowProc));

        AddIcon();
    }

    // ── INotificationService ──────────────────────────────────────────────

    /// <inheritdoc/>
    public void ShowNotification(string title, string body, NotificationType type = NotificationType.Info, string? actionUrl = null)
    {
        if (_disposed || !_iconAdded) return;

        _pendingActionUrl = actionUrl;

        var infoFlags = type switch
        {
            NotificationType.Chat => NiifInfo,
            NotificationType.Mention => NiifWarning,
            NotificationType.Warning => NiifWarning,
            NotificationType.Error => NiifError,
            _ => NiifInfo,
        } | NiifNosound;

        var data = BuildData();
        data.uFlags = NifInfo;
        data.szInfoTitle = title.Length > 63 ? title[..63] : title;
        data.szInfo = body.Length > 255 ? body[..255] : body;
        data.uTimeout = 5000;
        data.dwInfoFlags = infoFlags;

        if (!Shell_NotifyIcon(NimModify, ref data))
        {
            _logger.LogDebug("Shell_NotifyIcon(NIM_MODIFY) for balloon tip failed (error {Code}).",
                Marshal.GetLastWin32Error());
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void AddIcon()
    {
        var data = BuildData();
        data.uFlags = NifMessage | NifTip;
        data.uCallbackMessage = _callbackMessage;
        data.szTip = "DotNetCloud Sync";

        _iconAdded = Shell_NotifyIcon(NimAdd, ref data);
        if (!_iconAdded)
        {
            _logger.LogWarning("Shell_NotifyIcon(NIM_ADD) failed (error {Code}). Balloon notifications disabled.",
                Marshal.GetLastWin32Error());
        }
    }

    private void RemoveIcon()
    {
        if (!_iconAdded) return;
        var data = BuildData();
        Shell_NotifyIcon(NimDelete, ref data);
        _iconAdded = false;
    }

    private NotifyIconData BuildData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
        hWnd = _hWnd,
        uID = 1,
    };

    private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == _callbackMessage)
        {
            var eventCode = unchecked((int)lParam.ToInt64());
            if (eventCode == NinBalloonUserClick && !string.IsNullOrWhiteSpace(_pendingActionUrl))
            {
                TryOpenUrl(_pendingActionUrl);
                OnNotificationActivated?.Invoke(_pendingActionUrl);
            }
        }

        return _previousWindowProc != IntPtr.Zero
            ? CallWindowProc(_previousWindowProc, hWnd, msg, wParam, lParam)
            : IntPtr.Zero;
    }

    private void TryOpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to open URL from activated notification.");
        }
    }

    // ── Disposal ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        RemoveIcon();

        if (_hWnd != IntPtr.Zero && _previousWindowProc != IntPtr.Zero)
            SetWindowLongPtr(_hWnd, GwlpWndProc, _previousWindowProc);

        if (_hWnd != IntPtr.Zero)
            DestroyWindow(_hWnd);
    }
}
