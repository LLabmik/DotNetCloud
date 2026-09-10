using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Android.ViewModels;
using Microsoft.Maui.ApplicationModel;

namespace DotNetCloud.Client.Android.Views;

/// <summary>Channel list screen — shows all channels the user has access to.</summary>
public partial class ChannelListPage : ContentPage
{
    // Whether the welcome overlay ("Begin Chatting") has already been dismissed during the
    // CURRENT app run. Deliberately static (not a persisted Preference): Shell re-creates this
    // page when re-entering Chat from the drawer, so instance state is lost and the overlay
    // would wrongly reappear mid-session. A static resets when the app process restarts, so the
    // overlay shows once per app launch — and only once.
    private static bool _landingDismissedThisRun;

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

        // Already dismissed earlier in this run (e.g. the page was re-created by the drawer):
        // stay on the channel list instead of showing the welcome again.
        if (_landingDismissedThisRun)
        {
            HideLandingOverlay();
        }
    }

    /// <summary>
    /// Hides the landing overlay, revealing the chat interface below, and remembers the
    /// dismissal for the rest of the current app run.
    /// </summary>
    private void DismissLanding()
    {
        _landingDismissedThisRun = true;
        HideLandingOverlay();
    }

    /// <summary>Hides the landing overlay without recording a dismissal.</summary>
    private void HideLandingOverlay()
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
