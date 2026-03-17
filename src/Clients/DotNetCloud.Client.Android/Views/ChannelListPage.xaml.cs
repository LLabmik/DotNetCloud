using DotNetCloud.Client.Android.ViewModels;
using Microsoft.Maui.ApplicationModel;

namespace DotNetCloud.Client.Android.Views;

/// <summary>Channel list screen — shows all channels the user has access to.</summary>
public partial class ChannelListPage : ContentPage
{
    private readonly ChannelListViewModel _vm;

    /// <summary>Initializes a new <see cref="ChannelListPage"/>.</summary>
    public ChannelListPage(ChannelListViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        vm.ChannelSelected += OnChannelSelected;
    }

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.LoadChannelsCommand.CanExecute(null))
            _vm.LoadChannelsCommand.Execute(null);
    }

    private async void OnChannelSelected(object? sender, (Guid ChannelId, string Name) e)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
            Shell.Current.GoToAsync($"MessageList?channelId={e.ChannelId}&channelName={Uri.EscapeDataString(e.Name)}", animate: true));
    }
}
