using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Android.ViewModels;
using Microsoft.Maui.ApplicationModel;

namespace DotNetCloud.Client.Android.Views;

/// <summary>Channel list screen — shows all channels the user has access to.</summary>
public partial class ChannelListPage : ContentPage
{
    // Persisted so the welcome overlay ("Begin Chatting") is a one-time intro: Shell can
    // re-create this page when re-entering Chat from the drawer, and without persistence the
    // overlay would reappear instead of going straight to the channel list.
    private const string LandingDismissedKey = "chat_landing_dismissed";

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

        // Skip the welcome overlay once it has been dismissed, so tapping "Chat" in the
        // hamburger menu always lands on the channel list.
        if (Preferences.Default.Get(LandingDismissedKey, false))
        {
            DismissLanding(persist: false);
        }
    }

    /// <summary>Hides the landing overlay, revealing the chat interface below.</summary>
    /// <param name="persist">
    /// When <c>true</c> (default) the dismissal is remembered across page re-creation and app
    /// restarts so the welcome overlay is shown only once.
    /// </param>
    private void DismissLanding(bool persist = true)
    {
        LandingOverlay.IsVisible = false;
        LandingOverlay.InputTransparent = true;

        if (persist)
        {
            Preferences.Default.Set(LandingDismissedKey, true);
        }
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
