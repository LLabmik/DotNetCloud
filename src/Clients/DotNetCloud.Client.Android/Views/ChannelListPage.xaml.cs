using DotNetCloud.Client.Android.ViewModels;

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

    private void OnChannelSelected(object? sender, Guid channelId)
    {
        Shell.Current.GoToAsync($"MessageList?channelId={channelId}", animate: true);
    }
}
