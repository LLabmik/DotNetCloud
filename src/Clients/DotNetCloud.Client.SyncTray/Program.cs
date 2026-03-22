using System.Diagnostics;
using Avalonia;
using DotNetCloud.Client.SyncTray;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Single-instance enforcement
        var lockPath = GetSingletonLockPath();
        using var singletonLock = TryAcquireSingletonLock(lockPath);
        if (singletonLock is null)
        {
            // Another instance is running for this OS user.
            Debug.WriteLine($"DotNetCloud SyncTray is already running for this user (lock: {lockPath}).");
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnExplicitShutdown);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static string GetSingletonLockPath()
    {
        var lockDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DotNetCloud",
            "locks");

        Directory.CreateDirectory(lockDirectory);
        return Path.Combine(lockDirectory, "sync-tray.instance.lock");
    }

    private static FileStream? TryAcquireSingletonLock(string lockPath)
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
}
