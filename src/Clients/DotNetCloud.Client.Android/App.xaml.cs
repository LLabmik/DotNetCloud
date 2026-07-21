using Android.Content;
using Android.OS;
using Android.Util;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Client.Android;

/// <summary>MAUI application entry point.</summary>
public partial class App : Application
{
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;

    /// <summary>Initializes a new <see cref="App"/>.</summary>
    public App(IServerConnectionStore serverStore, ISecureTokenStore tokenStore)
    {
        InitializeComponent();
        _serverStore = serverStore;
        _tokenStore = tokenStore;

        // Force dark mode across the entire app
        UserAppTheme = AppTheme.Dark;
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState)
    {
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

        return window;
    }

    /// <inheritdoc />
    protected override async void OnStart()
    {
        base.OnStart();

        // Request POST_NOTIFICATIONS permission on Android 13+ so that
        // calendar reminders, chat messages, etc. are displayed.
        await RequestNotificationPermissionAsync();

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
                await Shell.Current.GoToAsync("//Main/ChannelList");
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

            var token = await _tokenStore.GetAccessTokenAsync(connection.ServerBaseUrl);
            if (token is null)
            {
                Log.Warn("DotNetCloud", "CheckAvailableModulesAsync: no token");
                return;
            }

            var baseUrl = connection.ServerBaseUrl.TrimEnd('/');

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

            var uploadIntent = new Intent(global::Android.App.Application.Context, typeof(MediaUploadForegroundService));
            uploadIntent.SetAction(MediaUploadForegroundService.ActionStart);
            global::Android.App.Application.Context.StartForegroundService(uploadIntent);
        }
    }
}
