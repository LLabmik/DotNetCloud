using Android.Content;
using Android.Util;
using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Calendar;
using DotNetCloud.Client.Android.ViewModels;
using Microsoft.Maui.ApplicationModel;

namespace DotNetCloud.Client.Android.Views;

/// <summary>Login screen — users enter a server URL and authenticate via OAuth2.</summary>
public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _vm;

    /// <summary>Initializes a new <see cref="LoginPage"/>.</summary>
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        vm.LoginSucceeded += OnLoginSucceeded;
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // If a saved server connection exists with a usable session, skip the login
        // page and go straight to the main page. This handles the case where Shell
        // resets to the default route (//Login) on Activity recreation after the app
        // is backgrounded. Crucially, we only redirect when the session can actually
        // be used — otherwise the first request 401s and the auth handler bounces us
        // right back here.
        try
        {
            var active = _vm.TryGetActiveConnection();
            if (active is not null && await App.HasUsableSessionAsync(active))
            {
                Log.Info("DotNetCloud", $"LoginPage.OnAppearing: saved connection found ({active}) with usable session, redirecting");
                _ = Shell.Current.GoToAsync("//Main/ChannelList");
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Warn("DotNetCloud", $"LoginPage.OnAppearing check failed: {ex.Message}");
        }

        // Auto-focus the server URL entry so the keyboard appears on page load.
        // Delayed to ensure the layout is ready on Android.
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), () => ServerUrlEntry.Focus());
    }

    private async void OnLoginSucceeded(object? sender, EventArgs e)
    {
        Log.Info("DotNetCloud", "LoginPage.OnLoginSucceeded: navigating to landing page");
        await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync("//Main/ChannelList", animate: true));
        Log.Info("DotNetCloud", "LoginPage.OnLoginSucceeded: navigation done, checking modules");

        // Check which optional server modules are available (Music, etc.)
        await App.CheckMusicModuleAvailabilityAsync();
        Log.Info("DotNetCloud", "LoginPage.OnLoginSucceeded: module check done");

        // Start the SignalR chat connection foreground service after successful login
        var intent = new Intent(global::Android.App.Application.Context, typeof(ChatConnectionService));
        intent.SetAction(ChatConnectionService.ActionStart);
        global::Android.App.Application.Context.StartForegroundService(intent);
        Log.Info("DotNetCloud", "LoginPage.OnLoginSucceeded: chat service started");

        // Start the calendar SignalR connection for real-time event notifications
        try
        {
            var serverUrl = _vm.ServerUrl?.TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(serverUrl))
            {
                var calSignalR = Ioc.Default.GetService<ICalendarSignalRClient>();
                if (calSignalR is not null)
                    _ = calSignalR.ConnectAsync(serverUrl);
            }
        }
        catch (Exception ex)
        {
            Log.Warn("DotNetCloud", $"LoginPage: failed to start calendar SignalR: {ex.Message}");
        }
    }
}
