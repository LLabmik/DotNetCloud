using DotNetCloud.Client.Android.ViewModels;

namespace DotNetCloud.Client.Android.Views;

/// <summary>Settings screen — shows account info and provides log out.</summary>
public partial class SettingsPage : ContentPage
{
    /// <summary>Initializes a new <see cref="SettingsPage"/>.</summary>
    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        vm.LoggedOut += OnLoggedOut;
    }

    private void OnLoggedOut(object? sender, EventArgs e)
    {
        Shell.Current.GoToAsync("//Login", animate: true);
    }
}
