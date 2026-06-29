using Android.Content;
using Android.Util;
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
    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Auto-focus the server URL entry so the keyboard appears on page load.
        // Delayed to ensure the layout is ready on Android.
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), () => ServerUrlEntry.Focus());
    }

    private async void OnLoginSucceeded(object? sender, EventArgs e)
    {
        Log.Info("DotNetCloud", "LoginPage.OnLoginSucceeded: navigating to channel list");
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
    }
}
