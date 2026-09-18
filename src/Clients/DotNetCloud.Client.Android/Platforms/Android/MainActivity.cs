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
    // Tracks whether the app is in the foreground so presence activity is only reported while
    // the user is actually looking at the app (a backgrounded phone must go idle/yellow).
    private bool _foreground;

    // Set on create and consumed by the first deep-link evaluation, so a notification tap that
    // cold-starts the app is deferred to App start-up while a warm tap navigates immediately.
    private bool _createdThisLaunch;

    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        _createdThisLaunch = true;
        base.OnCreate(savedInstanceState);
    }

    /// <inheritdoc />
    protected override void OnStart()
    {
        base.OnStart();
        // The chat foreground service is intentionally held only while the app is visible
        // (see OnStop). Restart it whenever the app returns to the foreground so SignalR
        // reconnects; message notifications while backgrounded are handled by FCM / UnifiedPush.
        TrySetChatServiceRunning(running: true);

        // Returning to the app is itself activity.
        _foreground = true;
        NotifyPresenceInteraction();
    }

    /// <inheritdoc />
    protected override void OnStop()
    {
        base.OnStop();
        // Stop the chat foreground service when the app is truly backgrounded (home,
        // recents, another app) so we never run an indefinite dataSync foreground service        // (Android 15+ caps these at 6h/day; Play efficiency rules target them). A
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
                StartService(intent);
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

        // Handle deep-link from notification tap (both cold start and resume). A cold start is
        // passed through because the Shell does not exist yet at this point.
        var coldStart = _createdThisLaunch;
        _createdThisLaunch = false;
        HandleNotificationDeepLink(coldStart);
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
        _foreground = false;
        try
        {
            Ioc.Default.GetService<IAppForegroundService>()?.SetForeground(false);
        }
        catch { /* Best effort */ }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Any real touch on the app is genuine user activity — report it (throttled server-side)
    /// so the user's presence stays green while they interact and goes yellow when idle.
    /// </remarks>
    public override bool DispatchTouchEvent(MotionEvent? ev)
    {
        if (_foreground && ev is not null && ev.Action == MotionEventActions.Down)
        {
            NotifyPresenceInteraction();
        }

        return base.DispatchTouchEvent(ev);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Keyboard/DPAD interaction also counts as genuine activity.
    /// </remarks>
    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        if (_foreground && e is not null && e.Action == KeyEventActions.Down)
        {
            NotifyPresenceInteraction();
        }

        return base.DispatchKeyEvent(e);
    }

    /// <summary>
    /// Notifies the presence activity reporter (best effort — never crashes the activity).
    /// </summary>
    private void NotifyPresenceInteraction()
    {
        try
        {
            Ioc.Default.GetService<IActivityReporter>()?.NotifyInteraction();
        }
        catch
        {
            // Best effort — presence reporting must never affect the UI path.
        }
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

    // ── Notification deep links ──────────────────────────────────────────────

    /// <summary>
    /// Routes a notification tap to the channel or event it refers to, on the server connection
    /// that sent it.
    /// </summary>
    /// <remarks>
    /// Notifications are generic by design, so the tap carries only identifiers plus the server
    /// the notification belongs to. When that server is not the active connection the active
    /// connection is switched first, so a tap can never open a channel on the wrong instance
    /// (plan §9.7). Extras are cleared before navigating so a later OnResume cannot repeat it.
    /// </remarks>
    /// <param name="coldStart">Whether this activity was created for the current launch.</param>
    private void HandleNotificationDeepLink(bool coldStart)
    {
        var channelId = Intent?.GetStringExtra(UnifiedPushNotificationRenderer.ExtraChannelId);
        var eventId = Intent?.GetStringExtra(UnifiedPushNotificationRenderer.ExtraEventId);
        var serverUrl = Intent?.GetStringExtra(UnifiedPushNotificationRenderer.ExtraServerUrl);

        if (string.IsNullOrWhiteSpace(channelId) && string.IsNullOrWhiteSpace(eventId))
            return;

        Intent?.RemoveExtra(UnifiedPushNotificationRenderer.ExtraChannelId);
        Intent?.RemoveExtra(UnifiedPushNotificationRenderer.ExtraEventId);
        Intent?.RemoveExtra(UnifiedPushNotificationRenderer.ExtraServerUrl);

        ActivateServerForNotification(serverUrl);

        var route = !string.IsNullOrWhiteSpace(channelId)
            // The channel name is resolved by the message list when only the id is known.
            ? $"MessageList?channelId={Uri.EscapeDataString(channelId)}&channelName="
            : $"EventDetail?EventId={Uri.EscapeDataString(eventId!)}";

        // A tap can cold-start the app, in which case the Shell does not exist yet and the
        // start-page navigation would land on top of our route: hand it to the app instead.
        if (coldStart)
        {
            App.PendingDeepLinkRoute = route;
            Log.Info("DotNetCloud", $"Deferring the notification deep link until start-up finishes ({route}).");
            return;
        }

        _ = Shell.Current?.GoToAsync(route);
    }

    /// <summary>Switches the active server connection when a notification came from another one.</summary>
    /// <param name="serverUrl">Server the notification belongs to, when the notification carried one.</param>
    private static void ActivateServerForNotification(string? serverUrl)
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
            return;

        try
        {
            var store = Ioc.Default.GetService<IServerConnectionStore>();
            var active = store?.GetActive();
            if (store is null || active is null)
                return;

            if (string.Equals(active.ServerBaseUrl.TrimEnd('/'), serverUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                return;

            var saved = store.GetAll().FirstOrDefault(connection => string.Equals(
                connection.ServerBaseUrl.TrimEnd('/'), serverUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));

            if (saved is null)
            {
                Log.Warn("DotNetCloud", $"Notification belongs to an unknown server connection ({serverUrl}).");
                return;
            }

            store.SetActive(saved.ServerBaseUrl);
            Log.Info("DotNetCloud", $"Switched the active connection to {saved.ServerBaseUrl} for a notification tap.");
        }
        catch (Exception ex)
        {
            Log.Warn("DotNetCloud", $"Switching the active connection for a notification failed: {ex.Message}");
        }
    }
}
