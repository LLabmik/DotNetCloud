using DotNetCloud.Client.Android.ViewModels;

namespace DotNetCloud.Client.Android.Views;

/// <summary>Event create/edit form page with full recurrence support.</summary>
public partial class EventEditPage : ContentPage
{
    private readonly EventEditViewModel _vm;

    /// <summary>Initializes a new <see cref="EventEditPage"/>.</summary>
    public EventEditPage(EventEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.Calendars.Count == 0)
            _vm.LoadCalendarsCommand.Execute(null);
    }

    private void OnSaveClicked(object? sender, EventArgs e) =>
        _vm.SaveCommand.Execute(null);

    private void OnCancelClicked(object? sender, EventArgs e) =>
        _vm.CancelCommand.Execute(null);

    private void OnDeleteClicked(object? sender, EventArgs e) =>
        _vm.DeleteCommand.Execute(null);

    /// <summary>Toggles a day-of-week for weekly recurrence via code-behind
    /// since binding to individual bools per button is impractical in XAML.</summary>
    private void OnDayToggled(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string day)
        {
            switch (day)
            {
                case "Sun":
                    _vm.RecurrenceSun = !_vm.RecurrenceSun;
                    break;
                case "Mon":
                    _vm.RecurrenceMon = !_vm.RecurrenceMon;
                    break;
                case "Tue":
                    _vm.RecurrenceTue = !_vm.RecurrenceTue;
                    break;
                case "Wed":
                    _vm.RecurrenceWed = !_vm.RecurrenceWed;
                    break;
                case "Thu":
                    _vm.RecurrenceThu = !_vm.RecurrenceThu;
                    break;
                case "Fri":
                    _vm.RecurrenceFri = !_vm.RecurrenceFri;
                    break;
                case "Sat":
                    _vm.RecurrenceSat = !_vm.RecurrenceSat;
                    break;
            }
            // Update button visual state
            button.BackgroundColor = GetDayToggleColor(day);
        }
    }

    private Color GetDayToggleColor(string day)
    {
        var isActive = day switch
        {
            "Sun" => _vm.RecurrenceSun,
            "Mon" => _vm.RecurrenceMon,
            "Tue" => _vm.RecurrenceTue,
            "Wed" => _vm.RecurrenceWed,
            "Thu" => _vm.RecurrenceThu,
            "Fri" => _vm.RecurrenceFri,
            "Sat" => _vm.RecurrenceSat,
            _ => false
        };
        return isActive ? Color.FromArgb("#0EA5E9") : Color.FromArgb("#1E293B");
    }
}
