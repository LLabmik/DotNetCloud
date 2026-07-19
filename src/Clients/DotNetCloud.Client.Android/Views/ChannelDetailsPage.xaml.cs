using DotNetCloud.Client.Android.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;

namespace DotNetCloud.Client.Android.Views;

/// <summary>
/// Code-behind for the channel details page.
/// Shows channel info, member list, mute toggle, and leave option.
/// </summary>
[QueryProperty(nameof(ChannelId), "channelId")]
[QueryProperty(nameof(ChannelDisplayName), "channelName")]
public partial class ChannelDetailsPage : ContentPage
{
    private readonly ChannelDetailsViewModel _vm;
    private ChannelListViewModel? _listVm;
    private Guid _channelId;
    private string _channelDisplayName = string.Empty;

    /// <summary>Injected channel ID from Shell navigation query parameter.</summary>
    public string ChannelId
    {
        set => _channelId = Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    /// <summary>Injected channel display name from Shell navigation query parameter.</summary>
    public string ChannelDisplayName
    {
        set => _channelDisplayName = value;
    }

    /// <summary>Initializes a new <see cref="ChannelDetailsPage"/>.</summary>
    public ChannelDetailsPage(ChannelDetailsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        vm.ChannelLeft += OnChannelLeft;
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            // Subscribe to mute state changes from the channel list
            _listVm = Handler?.MauiContext?.Services.GetService<ChannelListViewModel>();
            if (_listVm is not null)
                _listVm.MuteStateChanged += OnMuteStateChanged;

            _vm.Prepare(_channelId, _channelDisplayName);
            await _vm.LoadAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChannelDetailsPage] OnAppearing error: {ex}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_listVm is not null)
            _listVm.MuteStateChanged -= OnMuteStateChanged;
    }

    private void OnMuteStateChanged(object? sender, (Guid ChannelId, bool IsMuted) e)
    {
        if (_vm.ChannelId == e.ChannelId)
            _vm.IsMuted = e.IsMuted;
    }

    private async void OnChannelLeft(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync("//Main/ChannelList", animate: true));
    }
}
