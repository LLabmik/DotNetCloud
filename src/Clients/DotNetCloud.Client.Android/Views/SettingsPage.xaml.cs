using DotNetCloud.Client.Android.ViewModels;
using Microsoft.Maui.ApplicationModel;

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

    private async void OnLoggedOut(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync("//Login", animate: true));
    }
}
