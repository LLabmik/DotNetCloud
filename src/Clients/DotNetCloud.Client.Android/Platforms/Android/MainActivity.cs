using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Android.Views;
using AndroidX.Activity;
using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Primary Android activity. Handles launch, orientation changes, and serves as the MAUI host.
/// </summary>
[Activity(
    Label = "@string/app_name",
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    /// <inheritdoc />
    protected override void OnStart()
    {
        base.OnStart();
        // The chat foreground service is intentionally held only while the app is visible
        // (see OnStop). Restart it whenever the app returns to the foreground so SignalR
        // reconnects; message notifications while backgrounded are handled by FCM / UnifiedPush.
        TrySetChatServiceRunning(running: true);
    }

    /// <inheritdoc />
    protected override void OnStop()
    {
        base.OnStop();
        // Stop the chat foreground service when the app is truly backgrounded (home,
        // recents, another app) so we never run an indefinite dataSync foreground service
        // (Android 15+ caps these at 6h/day; Play efficiency rules target them). A
        // backgrounded app has no UI needing real-time refresh — push delivers notifications.
        TrySetChatServiceRunning(running: false);
    }

    /// <summary>
    /// Starts or stops <see cref="ChatConnectionService"/> based on app visibility.
    /// No-op when no server session is active (e.g., not logged in yet). Best-effort:
    /// a failure here must never crash the activity.
    /// </summary>
    private void TrySetChatServiceRunning(bool running)
    {
        try
        {
            if (Ioc.Default.GetService<IServerConnectionStore>()?.GetActive() is null)
                return;

            var intent = new Intent(this, typeof(ChatConnectionService));
            if (running)
            {
                intent.SetAction(ChatConnectionService.ActionStart);
                StartForegroundService(intent);
            }
            else
            {
                StopService(intent);
            }
        }
        catch (Exception ex)
        {
            Log.Warn("DotNetCloud", $"TrySetChatServiceRunning({running}) failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    protected override void OnResume()
    {
        base.OnResume();
        try
        {
            Ioc.Default.GetService<IAppForegroundService>()?.SetForeground(true);
        }
        catch { /* Best effort */ }

        // Handle deep-link from notification tap (both cold start and resume)
        HandleCalendarDeepLink();
    }

    /// <inheritdoc />
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (intent is not null)
            Intent = intent; // Ensure the latest intent is used for deep-link handling
    }

    /// <inheritdoc />
    protected override void OnPause()
    {
        base.OnPause();
        try
        {
            Ioc.Default.GetService<IAppForegroundService>()?.SetForeground(false);
        }
        catch { /* Best effort */ }
    }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        base.OnDestroy();
        try
        {
            CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default
                .GetService<DotNetCloud.Client.Android.Services.IMusicPlayerService>()?.Stop();
        }
        catch
        {
            // Best effort — process is shutting down
        }
    }

    // ── Calendar notification deep-link ─────────────────────────────────────

    private void HandleCalendarDeepLink()
    {
        var eventId = Intent?.GetStringExtra("eventId");
        if (string.IsNullOrWhiteSpace(eventId))
            return;

        // Clear the extra so we don't re-navigate on subsequent OnResume calls
        Intent?.RemoveExtra("eventId");

        _ = Shell.Current?.GoToAsync($"EventDetail?EventId={eventId}");
    }
}
