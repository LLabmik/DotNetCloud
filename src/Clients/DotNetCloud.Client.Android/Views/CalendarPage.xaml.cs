using DotNetCloud.Client.Android.ViewModels;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.Views;

/// <summary>Main Calendar tab page with month/week/day views, date navigation, and event creation.</summary>
public partial class CalendarPage : ContentPage
{
    private readonly CalendarViewModel _vm;

    /// <summary>Initializes a new <see cref="CalendarPage"/>.</summary>
    public CalendarPage(CalendarViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.IsActive = true;
        _vm.ErrorMessage = null;
        _vm.StartRefreshTimer();
        if (_vm.Calendars.Count == 0 && _vm.LoadCalendarsCommand.CanExecute(null))
        {
            _vm.LoadCalendarsCommand.Execute(null);
        }
        else if (_vm.LoadEventsCommand.CanExecute(null))
        {
            // Always reload events when the tab becomes visible, so events
            // created/updated/deleted from other clients (e.g. Blazor UI)
            // appear without requiring SignalR push to work.
            _vm.LoadEventsCommand.Execute(null);
        }
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.IsActive = false;
        _vm.ErrorMessage = null;
        _vm.StopRefreshTimer();
    }

    private void OnMonthClicked(object? sender, EventArgs e) =>
        _vm.SetViewCommand.Execute("Month");

    private void OnWeekClicked(object? sender, EventArgs e) =>
        _vm.SetViewCommand.Execute("Week");

    private void OnDayClicked(object? sender, EventArgs e) =>
        _vm.SetViewCommand.Execute("Day");

    private void OnPreviousClicked(object? sender, EventArgs e) =>
        _vm.PreviousPeriodCommand.Execute(null);

    private void OnNextClicked(object? sender, EventArgs e) =>
        _vm.NextPeriodCommand.Execute(null);

    private void OnTodayClicked(object? sender, EventArgs e) =>
        _vm.TodayCommand.Execute(null);

    private void OnCreateEventClicked(object? sender, EventArgs e) =>
        _vm.CreateEventCommand.Execute(null);

    private void OnDaySelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is CalendarDayItem dayItem && dayItem.Events.Count == 1)
        {
            _vm.SelectEventCommand.Execute(dayItem.Events[0]);
        }
        // Clear selection to allow re-selecting the same item
        if (sender is CollectionView cv)
            cv.SelectedItem = null;
    }

    private void OnEventSelected(object? sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is CalendarEventDto evt)
            {
                _vm.SelectEventCommand.Execute(evt);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnEventSelected error: {ex.Message}");
        }
        finally
        {
            if (sender is CollectionView cv)
                cv.SelectedItem = null;
        }
    }
}
