using DotNetCloud.Client.Android.ViewModels;

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

    private void OnLoginSucceeded(object? sender, EventArgs e)
    {
        // Replace the entire navigation stack with the channel list
        Shell.Current.GoToAsync("//ChannelList", animate: true);
    }
}
