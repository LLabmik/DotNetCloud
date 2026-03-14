using System.Diagnostics;
using Avalonia;
using DotNetCloud.Client.SyncTray;

// ── Single-instance enforcement ──────────────────────────────────────────────

var lockPath = GetSingletonLockPath();
using var singletonLock = TryAcquireSingletonLock(lockPath);
if (singletonLock is null)
{
    // Another instance is running for this OS user.
    Debug.WriteLine($"DotNetCloud SyncTray is already running for this user (lock: {lockPath}).");
    return;
}

// ── Avalonia app ─────────────────────────────────────────────────────────────

BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnExplicitShutdown);

return;

static AppBuilder BuildAvaloniaApp() =>
    AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();

static string GetSingletonLockPath()
{
    var lockDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DotNetCloud",
        "locks");

    Directory.CreateDirectory(lockDirectory);
    return Path.Combine(lockDirectory, "sync-tray.instance.lock");
}

static FileStream? TryAcquireSingletonLock(string lockPath)
{
    try
    {
        // Keep this stream open for process lifetime to hold the singleton lock.
        return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    catch (IOException)
    {
        return null;
    }
}
