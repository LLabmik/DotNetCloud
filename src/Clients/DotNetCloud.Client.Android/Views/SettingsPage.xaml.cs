using DotNetCloud.Client.Android.ViewModels;
using Microsoft.Maui.ApplicationModel;

namespace DotNetCloud.Client.Android.Views;

/// <summary>Settings screen — shows account info, file sync settings, battery optimization, and log out.</summary>
public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _vm;

    /// <summary>Initializes a new <see cref="SettingsPage"/>.</summary>
    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        vm.LoggedOut += OnLoggedOut;
    }

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.RefreshBatteryStatus();
    }

    private async void OnLoggedOut(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync("//Login", animate: true));
    }
}
