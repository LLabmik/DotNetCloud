using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Android.ViewModels;
using Microsoft.Maui.ApplicationModel;

namespace DotNetCloud.Client.Android.Views;

/// <summary>Channel list screen — shows all channels the user has access to.</summary>
public partial class ChannelListPage : ContentPage
{
    private readonly ChannelListViewModel _vm;

    /// <summary>Initializes a new <see cref="ChannelListPage"/>.</summary>
    public ChannelListPage(ChannelListViewModel vm, IServerConnectionStore serverStore)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        vm.ChannelSelected += OnChannelSelected;
        vm.DmCreated += OnDmCreated;

        // Show the connected server URL on the landing overlay
        var connection = serverStore.GetActive();
        ServerUrlLabel.Text = connection?.ServerBaseUrl ?? string.Empty;
    }

    /// <summary>Hides the landing overlay, revealing the chat interface below.</summary>
    private void DismissLanding()
    {
        LandingOverlay.IsVisible = false;
        LandingOverlay.InputTransparent = true;
    }

    private void OnBeginChattingClicked(object? sender, EventArgs e)
    {
        DismissLanding();
    }

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.IsActive = true;
        _vm.ErrorMessage = null;
        if (_vm.LoadChannelsCommand.CanExecute(null))
            _vm.LoadChannelsCommand.Execute(null);
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.IsActive = false;
        _vm.ErrorMessage = null;

        // Hide the landing overlay permanently the first time the user
        // navigates to another tab (OnDisappearing fires when switching tabs).
        DismissLanding();
    }

    private async void OnChannelSelected(object? sender, (Guid ChannelId, string Name) e)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
            Shell.Current.GoToAsync($"MessageList?channelId={e.ChannelId}&channelName={Uri.EscapeDataString(e.Name)}", animate: true));
    }

    private async void OnDmCreated(object? sender, (Guid ChannelId, string Name) e)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Shell.Current.Navigation.PopModalAsync(animated: true);
            await Shell.Current.GoToAsync($"MessageList?channelId={e.ChannelId}&channelName={Uri.EscapeDataString(e.Name)}", animate: true);
        });
    }

    private async void OnNewDmClicked(object? sender, EventArgs e)
    {
        _vm.OpenDmPickerCommand.Execute(null);
        var pickerPage = new DmUserPickerPage(_vm);
        await Navigation.PushModalAsync(pickerPage, animated: true);
    }
}
