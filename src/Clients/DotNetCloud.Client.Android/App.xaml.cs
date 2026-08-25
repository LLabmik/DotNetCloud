using Android.Content;
using Android.OS;
using Android.Util;
using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Calendar;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Android.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;

namespace DotNetCloud.Client.Android;

/// <summary>MAUI application entry point.</summary>
public partial class App : Application
{
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly IOfflineSyncService _offlineSync;

    /// <summary>Initializes a new <see cref="App"/>.</summary>
    public App(IServerConnectionStore serverStore, ISecureTokenStore tokenStore, IOfflineSyncService offlineSync)
    {
        InitializeComponent();
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _offlineSync = offlineSync;

        // Force dark mode across the entire app
        UserAppTheme = AppTheme.Dark;
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState)
    {
        // The Shell MUST be the window's page. MAUI forbids nesting a Page (and Shell
        // derives from Page) inside a Layout — "Parent of a Page must also be a Page" —
        // so the global offline banner cannot wrap the Shell in a Grid. Instead it is
        // attached as a native Android overlay above the activity content root
        // (see <see cref="SetupOfflineBannerOverlay"/>), which floats above every page.
        var window = new Window(new AppShell());

        window.Destroying += (s, e) =>
        {
            try
            {
                var player = window.Page?.Handler?.MauiContext?.Services
                    .GetService<IMusicPlayerService>();
                player?.Stop();
            }
            catch
            {
                // Best effort — process is dying anyway
            }
        };

        window.Created += (_, _) => SetupOfflineBannerOverlay();

        return window;
    }

    /// <summary>
    /// Tag used to locate the offline banner view on the activity content root.
    /// </summary>
    private const string OfflineBannerTag = "dotnetcloud-offline-banner";

    /// <summary>
    /// Attaches the global "server offline" banner as a native Android overlay pinned to
    /// the top of the activity content. Driven by <see cref="ConnectivityViewModel"/> so
    /// it appears whenever the reachability service reports the server as unreachable and
    /// clears automatically on recovery. Best-effort: a failure here must never crash the app.
    /// </summary>
    private void SetupOfflineBannerOverlay()
    {
        Dispatcher.Dispatch(() =>
        {
            try
            {
                var connectivity = Ioc.Default.GetService<ConnectivityViewModel>();
                var activity = Platform.CurrentActivity;
                if (connectivity is null || activity is null)
                    return;

                var content = activity.FindViewById<global::Android.Views.ViewGroup>(global::Android.Resource.Id.Content);
                if (content is null || content.FindViewWithTag(OfflineBannerTag) is not null)
                    return;

                var density = activity.Resources?.DisplayMetrics?.Density ?? 1f;
                var banner = new global::Android.Widget.TextView(activity)
                {
                    Tag = OfflineBannerTag,
                    Text = "Can't reach server — showing cached data. Changes will be queued.",
                    Gravity = global::Android.Views.GravityFlags.Center,
                    Visibility = connectivity.IsServerOffline
                        ? global::Android.Views.ViewStates.Visible
                        : global::Android.Views.ViewStates.Gone,
                };
                banner.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#B91C1C"));
                banner.SetTextColor(global::Android.Graphics.Color.White);
                banner.SetTextSize(global::Android.Util.ComplexUnitType.Sp, 12f);
                banner.SetPadding(
                    (int)(12 * density), (int)(6 * density),
                    (int)(12 * density), (int)(6 * density));

                var bannerLayout = new global::Android.Widget.FrameLayout.LayoutParams(
                    global::Android.Widget.FrameLayout.LayoutParams.MatchParent,
                    global::Android.Widget.FrameLayout.LayoutParams.WrapContent,
                    global::Android.Views.GravityFlags.Top);
                content.AddView(banner, bannerLayout);

                // With edge-to-edge MAUI layouts the content view spans behind the status
                // bar, so offset the banner below it or the text overlaps the clock,
                // battery and notification icons.
                banner.Post(() =>
                {
                    try
                    {
                        var statusBarHeight = ResolveStatusBarHeight(activity, banner);
                        if (statusBarHeight > 0)
                        {
                            bannerLayout.TopMargin = statusBarHeight;
                            banner.LayoutParameters = bannerLayout;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("DotNetCloud", $"Offline banner status-bar offset failed: {ex.Message}");
                    }
                });

                connectivity.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName != nameof(ConnectivityViewModel.IsServerOffline))
                        return;

                    activity.RunOnUiThread(() =>
                        banner.Visibility = connectivity.IsServerOffline
                            ? global::Android.Views.ViewStates.Visible
                            : global::Android.Views.ViewStates.Gone);
                };
            }
            catch (Exception ex)
            {
                Log.Warn("DotNetCloud", $"Offline banner overlay setup failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Resolves the current status bar height in pixels so the global offline banner sits
    /// below the system status bar instead of overlapping the clock/battery icons.
    /// Prefers the runtime window insets (handles display cutouts); falls back to the
    /// platform <c>status_bar_height</c> dimension.
    /// </summary>
    private static int ResolveStatusBarHeight(
        global::Android.App.Activity activity,
        global::Android.Views.View view)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            var insets = view.RootWindowInsets;
            if (insets is not null)
            {
                var top = insets.GetInsets(global::Android.Views.WindowInsets.Type.StatusBars()).Top;
                if (top > 0)
                    return top;
            }
        }

        var resourceId = activity.Resources?.GetIdentifier(
            "status_bar_height", "dimen", "android") ?? 0;
        return resourceId > 0
            ? activity.Resources!.GetDimensionPixelSize(resourceId)
            : 0;
    }

    /// <inheritdoc />
    protected override async void OnStart()
    {
        base.OnStart();

        // Request POST_NOTIFICATIONS permission on Android 13+ so that
        // calendar reminders, chat messages, etc. are displayed.
        await RequestNotificationPermissionAsync();

        // Start connectivity monitoring and flush any operations queued from a previous
        // offline session as soon as the device is online.
        try
        {
            await _offlineSync.StartAsync();
        }
        catch (Exception ex)
        {
            Log.Warn("DotNetCloud", $"OfflineSync start failed: {ex.Message}");
        }

        // Start server reachability monitoring so the global offline banner reflects
        // "server unreachable" (distinct from device-internet) and the offline queue
        // flushes automatically when the server returns.
        try
        {
            Ioc.Default.GetService<IServerReachabilityService>()?.Start();
        }
        catch (Exception ex)
        {
            Log.Warn("DotNetCloud", $"Reachability start failed: {ex.Message}");
        }

        // Warm up the access token before the first authenticated request so the session
        // never starts with an expired token (prevents 401 → re-login loops).
        try
        {
            var active = _serverStore.GetActive();
            if (active is not null)
            {
                var tokenRefresh = Ioc.Default.GetService<ITokenRefreshService>();
                if (tokenRefresh is not null)
                    await tokenRefresh.EnsureFreshAccessTokenAsync(active.ServerBaseUrl);
            }
        }
        catch (Exception ex)
        {
            Log.Warn("DotNetCloud", $"Token warm-up failed: {ex.Message}");
        }

        await CheckAvailableModulesAsync();
        await NavigateToStartPageAsync();
    }

    /// <summary>
    /// Requests <c>POST_NOTIFICATIONS</c> runtime permission on Android 13+.
    /// On older API levels, this returns immediately.
    /// </summary>
    private static async Task RequestNotificationPermissionAsync()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
            return; // No runtime permission needed before Android 13

        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            Log.Info("DotNetCloud", $"POST_NOTIFICATIONS status: {status}");

            if (status != PermissionStatus.Granted)
            {
                Log.Info("DotNetCloud", "Requesting POST_NOTIFICATIONS permission...");
                status = await Permissions.RequestAsync<Permissions.PostNotifications>();
                Log.Info("DotNetCloud", $"POST_NOTIFICATIONS request result: {status}");
            }
        }
        catch (Exception ex)
        {
            Log.Warn("DotNetCloud", $"POST_NOTIFICATIONS request failed: {ex.Message}");
        }
    }

    protected override async void OnResume()
    {
        base.OnResume();
        try
        {
            var active = _serverStore.GetActive();
            var location = Shell.Current?.CurrentState?.Location?.ToString();
            if (active is not null && location is not null && location.Contains("Login") && Shell.Current is not null)
            {
                // Only skip login if we actually hold a usable (or refreshable) session.
                // Otherwise the user would be bounced straight back here on the first 401.
                if (await HasUsableSessionAsync(active.ServerBaseUrl))
                {
                    await Shell.Current.GoToAsync("//Main/ChannelList");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn("DotNetCloud", $"OnResume redirect error: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks whether the Music module is available on the connected server and
    /// shows/hides the Music tab accordingly. Called at startup and after login.
    /// </summary>
    public static async Task CheckMusicModuleAvailabilityAsync()
    {
        Log.Info("DotNetCloud", "CheckMusicModuleAvailabilityAsync called");
        if (Application.Current is not App app)
        {
            Log.Warn("DotNetCloud", "CheckMusicModuleAvailabilityAsync: Application.Current is not App");
            return;
        }
        await app.CheckAvailableModulesAsync();
    }

    /// <summary>
    /// Triggers a full rescan of all optional modules on the connected server.
    /// Clears any cached availability state, re-checks every known module, and
    /// updates tab visibility accordingly. Call this from the Settings "Rescan Modules" action.
    /// </summary>
    public static async Task TriggerModuleRescanAsync()
    {
        Log.Info("DotNetCloud", "TriggerModuleRescanAsync called");
        if (Application.Current is not App app)
        {
            Log.Warn("DotNetCloud", "TriggerModuleRescanAsync: Application.Current is not App");
            return;
        }

        ModuleAvailabilityState.ClearAll();
        await app.CheckAvailableModulesAsync();
        AppShell.RefreshAllTabs();
        Log.Info("DotNetCloud", "TriggerModuleRescanAsync completed");
    }

    /// <summary>
    /// Returns whether the app holds a usable session for the given server: a stored
    /// access token that is not expired, or (if expired) one that can be refreshed right
    /// now. When the server is unreachable, a stored token is treated as usable so the
    /// app can keep working offline from cached data instead of stranding the user on
    /// the login screen.
    /// </summary>
    public static async Task<bool> HasUsableSessionAsync(string serverUrl)
    {
        try
        {
            var tokenStore = Ioc.Default.GetService<ISecureTokenStore>();
            if (tokenStore is null)
                return false;

            var accessToken = await tokenStore.GetAccessTokenAsync(serverUrl);
            if (string.IsNullOrWhiteSpace(accessToken))
                return false;

            var expiry = await tokenStore.GetAccessTokenExpiryAsync(serverUrl);
            if (expiry is null || DateTimeOffset.UtcNow < expiry.Value)
                return true;

            // Token is expired. If the server is currently unreachable, enter the app
            // anyway (offline/cached mode) rather than blocking on a login that can't
            // complete without network.
            var reachability = Ioc.Default.GetService<IServerReachabilityService>();
            if (reachability is not null && !reachability.IsServerOnline)
                return true;

            var tokenRefresh = Ioc.Default.GetService<ITokenRefreshService>();
            var fresh = tokenRefresh is not null
                ? await tokenRefresh.EnsureFreshAccessTokenAsync(serverUrl)
                : null;
            return !string.IsNullOrWhiteSpace(fresh);
        }
        catch
        {
            return false;
        }
    }

    private async Task CheckAvailableModulesAsync()
    {
        Log.Info("DotNetCloud", "CheckAvailableModulesAsync started");
        try
        {
            var connection = _serverStore.GetActive();
            if (connection is null)
            {
                Log.Warn("DotNetCloud", "CheckAvailableModulesAsync: no active connection");
                return;
            }

            var tokenRefresh = Ioc.Default.GetService<ITokenRefreshService>();
            var token = tokenRefresh is not null
                ? await tokenRefresh.EnsureFreshAccessTokenAsync(connection.ServerBaseUrl)
                : await _tokenStore.GetAccessTokenAsync(connection.ServerBaseUrl);
            if (token is null)
            {
                Log.Warn("DotNetCloud", "CheckAvailableModulesAsync: no token");
                return;
            }

            var baseUrl = connection.ServerBaseUrl.TrimEnd('/');

            // ── Start calendar SignalR connection for real-time event sync ──
            try
            {
                var calSignalR = Ioc.Default.GetService<ICalendarSignalRClient>();
                Log.Info("DotNetCloud", $"CheckAvailableModules: CalendarSignalR client resolved: {(calSignalR is not null)}");
                if (calSignalR is not null)
                {
                    Log.Info("DotNetCloud", "CheckAvailableModules: starting CalendarSignalR connection...");
                    _ = calSignalR.ConnectAsync(baseUrl, token);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("DotNetCloud", $"CheckAvailableModules: calendar SignalR start failed: {ex.Message}");
            }

            // ── Check Music module ──────────────────────────────────
            // Try the official module availability endpoint first.
            var musicAvailable = await CheckMusicModuleEndpointAsync(baseUrl, token);

            // Fallback: probe an actual music API endpoint to double-check
            // (the module may be running but not registered in the core module registry).
            if (!musicAvailable)
            {
                musicAvailable = await ProbeMusicApiAsync(baseUrl, token);
            }

            Log.Info("DotNetCloud", $"CheckAvailableModulesAsync: musicAvailable={musicAvailable}");
            ModuleAvailabilityState.SetMusicAvailable(musicAvailable);
            if (musicAvailable)
            {
                AppShell.SetMusicTabVisible(true);
                Log.Info("DotNetCloud", "CheckAvailableModulesAsync: Music tab set visible");
            }

            // ── Future modules: add additional checks here ──────────
            // Each new module follows the same pattern:
            //   1. Check the module availability endpoint
            //   2. Optionally probe a known API endpoint as fallback
            //   3. Call ModuleAvailabilityState.SetModuleAvailable("ModuleName", result)
            //   4. Call AppShell.SetXxxTabVisible(result) if applicable
        }
        catch (Exception ex)
        {
            Log.Error("DotNetCloud", $"CheckAvailableModulesAsync: exception: {ex.Message}");
            ModuleAvailabilityState.SetMusicAvailable(false);
            AppShell.SetMusicTabVisible(false);
        }
    }

    private static async Task<bool> CheckMusicModuleEndpointAsync(string baseUrl, string token)
    {
        try
        {
            var url = $"{baseUrl}/api/v1/core/modules/music/available";
            Log.Info("DotNetCloud", $"CheckMusicModuleEndpoint: GET {url}");
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await http.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            Log.Info("DotNetCloud", $"CheckMusicModuleEndpoint: status={(int)response.StatusCode} body={body}");

            if (!response.IsSuccessStatusCode)
                return false;

            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("installed", out var installed))
            {
                return installed.GetBoolean();
            }
            return false;
        }
        catch (Exception ex)
        {
            Log.Warn("DotNetCloud", $"CheckMusicModuleEndpoint: exception {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> ProbeMusicApiAsync(string baseUrl, string token)
    {
        try
        {
            // Try the artists endpoint with take=1. If the module is installed,
            // this returns 200 (even with empty results). If not installed, the
            // server returns 404 or the request is routed differently.
            var url = $"{baseUrl}/api/v1/music/artists?skip=0&take=1";
            Log.Info("DotNetCloud", $"ProbeMusicApi: GET {url}");
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await http.GetAsync(url);
            var status = (int)response.StatusCode;
            var body = await response.Content.ReadAsStringAsync();
            Log.Info("DotNetCloud", $"ProbeMusicApi: status={status} body={body}");

            // 200 or 401 means the endpoint exists (module is installed).
            // 404 means the endpoint doesn't exist (module not installed).
            return status == 200 || status == 401;
        }
        catch (Exception ex)
        {
            Log.Warn("DotNetCloud", $"ProbeMusicApi: exception {ex.Message}");
            return false;
        }
    }

    private async Task NavigateToStartPageAsync()
    {
        if (_serverStore.GetActive() is not null)
        {
            await Shell.Current.GoToAsync("//Main/ChannelList");

            var chatIntent = new Intent(global::Android.App.Application.Context, typeof(ChatConnectionService));
            chatIntent.SetAction(ChatConnectionService.ActionStart);
            global::Android.App.Application.Context.StartForegroundService(chatIntent);

            // Only start the media upload foreground service if the user has enabled auto-upload.
            if (Preferences.Default.Get("media_upload_enabled", false))
            {
                var uploadIntent = new Intent(global::Android.App.Application.Context, typeof(MediaUploadForegroundService));
                uploadIntent.SetAction(MediaUploadForegroundService.ActionStart);
                global::Android.App.Application.Context.StartForegroundService(uploadIntent);
            }
        }
    }
}
