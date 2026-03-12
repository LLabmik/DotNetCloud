using DotNetCloud.Client.Android.ViewModels;

namespace DotNetCloud.Client.Android.Views;

/// <summary>
/// Code-behind for the channel details page.
/// Shows channel info, member list, mute toggle, and leave option.
/// </summary>
public partial class ChannelDetailsPage : ContentPage
{
    private readonly ChannelDetailsViewModel _vm;

    /// <summary>Initializes a new <see cref="ChannelDetailsPage"/>.</summary>
    public ChannelDetailsPage(ChannelDetailsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync().ConfigureAwait(false);
    }
}
