using Android.Content;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Services;

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
        return new Window(new AppShell());
    }

    /// <inheritdoc />
    protected override async void OnStart()
    {
        base.OnStart();
        await CheckAvailableModulesAsync();
        await NavigateToStartPageAsync();
    }

    /// <summary>
    /// Checks whether the Music module is available on the connected server and
    /// shows/hides the Music tab accordingly. Called at startup and after login.
    /// </summary>
    public static async Task CheckMusicModuleAvailabilityAsync()
    {
        if (Application.Current is not App app) return;
        await app.CheckAvailableModulesAsync();
    }

    private async Task CheckAvailableModulesAsync()
    {
        try
        {
            var connection = _serverStore.GetActive();
            if (connection is null) return;

            var token = await _tokenStore.GetAccessTokenAsync(connection.ServerBaseUrl);
            if (token is null) return;

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var url = $"{connection.ServerBaseUrl.TrimEnd('/')}/api/v1/core/modules/music/available";
            var response = await http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return;

            var body = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("installed", out var installed))
            {
                Services.ModuleAvailabilityState.SetMusicAvailable(installed.GetBoolean());
                AppShell.SetMusicTabVisible(true);
            }
        }
        catch
        {
            Services.ModuleAvailabilityState.SetMusicAvailable(false);
            AppShell.SetMusicTabVisible(false);
        }
    }

    private async Task NavigateToStartPageAsync()
    {
        // Navigate to the channel list if a server connection is already active,
        // otherwise drop the user on the login screen.
        if (_serverStore.GetActive() is not null)
        {
            await Shell.Current.GoToAsync("//Main/ChannelList");

            // Start SignalR chat foreground service when resuming with saved session
            var intent = new Intent(global::Android.App.Application.Context, typeof(ChatConnectionService));
            intent.SetAction(ChatConnectionService.ActionStart);
            global::Android.App.Application.Context.StartForegroundService(intent);
        }
        else
        {
            await Shell.Current.GoToAsync("//Login");
        }
    }
}
