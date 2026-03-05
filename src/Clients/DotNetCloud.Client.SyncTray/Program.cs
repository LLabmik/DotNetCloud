using System.Diagnostics;
using Avalonia;
using DotNetCloud.Client.SyncTray;

// ── Single-instance enforcement ──────────────────────────────────────────────

const string MutexName = "Global\\DotNetCloud.SyncTray.Instance";
using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
if (!createdNew)
{
    // Another instance is running — nothing to do (future: bring its window to front via IPC)
    Debug.WriteLine("DotNetCloud SyncTray is already running.");
    return;
}

// ── Avalonia app ─────────────────────────────────────────────────────────────

try
{
    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnExplicitShutdown);
}
finally
{
    mutex.ReleaseMutex();
}

return;

static AppBuilder BuildAvaloniaApp() =>
    AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
