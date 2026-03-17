using DotNetCloud.Client.Android.ViewModels;
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
        _vm.Prepare(_channelId, _channelDisplayName);
        await _vm.LoadAsync();
    }

    private async void OnChannelLeft(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync("//Main/ChannelList", animate: true));
    }
}
