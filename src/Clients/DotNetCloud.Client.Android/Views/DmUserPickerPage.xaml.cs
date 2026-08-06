using DotNetCloud.Client.Android.ViewModels;

namespace DotNetCloud.Client.Android.Views;

/// <summary>
/// Modal page for searching and selecting users to start a direct message.
/// Binds directly to the parent ChannelListViewModel's DM state.
/// </summary>
public partial class DmUserPickerPage : ContentPage
{
    private readonly ChannelListViewModel _vm;

    /// <summary>Initializes a new <see cref="DmUserPickerPage"/>.</summary>
    public DmUserPickerPage(ChannelListViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        _vm.CloseDmPickerCommand.Execute(null);
        Navigation.PopModalAsync(animated: true);
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _vm.SearchDmUsersCommand.Execute(e.NewTextValue);
    }
}
