using DotNetCloud.Client.Android.ViewModels;
using Microsoft.Maui.ApplicationModel;

namespace DotNetCloud.Client.Android.Views;

/// <summary>Message list screen — real-time chat for a single channel.</summary>
[QueryProperty(nameof(ChannelId), "channelId")]
[QueryProperty(nameof(ChannelDisplayName), "channelName")]
public partial class MessageListPage : ContentPage
{
    private readonly MessageListViewModel _vm;
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

    /// <summary>Initializes a new <see cref="MessageListPage"/>.</summary>
    public MessageListPage(MessageListViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        vm.ViewDetailsRequested += OnViewDetailsRequested;
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _vm.InitializeAsync(_channelId, _channelDisplayName);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MessageListPage] OnAppearing error: {ex}");
        }
    }

    private async void OnViewDetailsRequested(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
            Shell.Current.GoToAsync(
                $"ChannelDetails?channelId={_channelId}&channelName={Uri.EscapeDataString(_channelDisplayName)}",
                animate: true));
    }
}
