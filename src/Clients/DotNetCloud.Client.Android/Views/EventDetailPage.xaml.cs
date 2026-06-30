using DotNetCloud.Client.Android.ViewModels;

namespace DotNetCloud.Client.Android.Views;

/// <summary>Event detail page — shows full event info, RSVP, and edit/delete actions.</summary>
public partial class EventDetailPage : ContentPage
{
    private readonly EventDetailViewModel _vm;

    /// <summary>Initializes a new <see cref="EventDetailPage"/>.</summary>
    public EventDetailPage(EventDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    private void OnBackClicked(object? sender, EventArgs e) =>
        _ = Shell.Current.GoToAsync("..");

    private void OnEditClicked(object? sender, EventArgs e) =>
        _vm.EditEventCommand.Execute(null);

    private void OnDeleteClicked(object? sender, EventArgs e) =>
        _vm.DeleteEventCommand.Execute(null);

    private void OnRsvpAccepted(object? sender, EventArgs e) =>
        _vm.RsvpCommand.Execute("Accepted");

    private void OnRsvpTentative(object? sender, EventArgs e) =>
        _vm.RsvpCommand.Execute("Tentative");

    private void OnRsvpDeclined(object? sender, EventArgs e) =>
        _vm.RsvpCommand.Execute("Declined");
}
