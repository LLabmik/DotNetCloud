using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Calendar;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Android.ViewModels;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Client.Android.Tests.ViewModels;

/// <summary>
/// Tests for the Calendar tab's day handling: a day cell holding a single event still opens that
/// event straight away, while a day cell holding several opens the day list so the user can pick
/// one (the app used to do nothing at all for a multi-event day).
/// </summary>
[TestClass]
public sealed class CalendarViewModelTests
{
    private static readonly Guid CalendarId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly Mock<ICalendarRestClient> _calendarApi = new();
    private CalendarViewModel _vm = null!;

    [TestInitialize]
    public void Setup()
    {
        var serverStore = new Mock<IServerConnectionStore>();
        serverStore.Setup(s => s.GetActive())
            .Returns(new ServerConnection("https://cloud.example.com", "Test", "user@example.com"));

        var tokenStore = new Mock<ISecureTokenStore>();
        tokenStore.Setup(s => s.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("access-token");

        var scheduler = new Mock<ICalendarReminderScheduler>();
        scheduler.Setup(s => s.ScheduleRemindersAsync(It.IsAny<IReadOnlyList<CalendarEventDto>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _calendarApi.Setup(c => c.ListEventsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CalendarEventDto>());

        _vm = new CalendarViewModel(
            _calendarApi.Object,
            serverStore.Object,
            tokenStore.Object,
            scheduler.Object,
            new Mock<ICalendarSignalRClient>().Object,
            NullLogger<CalendarViewModel>.Instance);

        _vm.Calendars.Add(new CalendarItem
        {
            Id = CalendarId,
            Name = "Work",
            Color = "#3B82F6",
            IsVisible = true
        });
    }

    [TestCleanup]
    public void Cleanup() => _vm.Dispose();

    // ── Day cell tap ───────────────────────────────────────────────

    [TestMethod]
    public void SelectDay_WithMultipleEvents_OpensDayListOrderedByStartTime()
    {
        var day = DateTime.Today;
        var early = NewEvent("Standup", day.AddHours(9), TimeSpan.FromHours(1));
        var late = NewEvent("Review", day.AddHours(16), TimeSpan.FromHours(1));
        var middle = NewEvent("Lunch", day.AddHours(12), TimeSpan.FromHours(1));

        _vm.SelectDayCommand.Execute(Day(day, late, early, middle));

        Assert.IsTrue(_vm.IsDayListVisible);
        Assert.AreEqual(day, _vm.SelectedDay);
        CollectionAssert.AreEqual(
            new[] { early, middle, late },
            _vm.SelectedDayEvents.ToArray(),
            "The day list must be ordered by start time.");
        Assert.AreEqual("3 events", _vm.SelectedDayCountLabel);
    }

    [TestMethod]
    public void SelectDay_WithMultipleEvents_SetsDayHeader()
    {
        var day = DateTime.Today.AddDays(3);
        var first = NewEvent("One", day.AddHours(9), TimeSpan.FromHours(1));
        var second = NewEvent("Two", day.AddHours(11), TimeSpan.FromHours(1));
        var third = NewEvent("Three", day.AddHours(13), TimeSpan.FromHours(1));

        _vm.SelectDayCommand.Execute(Day(day, first, second, third));

        Assert.AreEqual(day.ToString("dddd, MMMM d, yyyy"), _vm.SelectedDayLabel);
        Assert.AreEqual("3 events", _vm.SelectedDayCountLabel);
    }

    [TestMethod]
    public void SelectDay_WithSingleEvent_OpensEventDetailsWithoutDayList()
    {
        var only = NewEvent("Solo", DateTime.Today.AddHours(10), TimeSpan.FromHours(1));

        _vm.SelectDayCommand.Execute(Day(DateTime.Today, only));

        Assert.IsFalse(_vm.IsDayListVisible);
        Assert.AreSame(only, _vm.SelectedEvent);
    }

    [TestMethod]
    public void SelectDay_WithNoEvents_DoesNothing()
    {
        _vm.SelectDayCommand.Execute(Day(DateTime.Today));

        Assert.IsFalse(_vm.IsDayListVisible);
        Assert.IsNull(_vm.SelectedEvent);
        Assert.AreEqual(0, _vm.SelectedDayEvents.Count);
    }

    [TestMethod]
    public void SelectDay_NullDay_DoesNothing()
    {
        _vm.SelectDayCommand.Execute(null);

        Assert.IsFalse(_vm.IsDayListVisible);
        Assert.IsNull(_vm.SelectedEvent);
    }

    [TestMethod]
    public void SelectDay_WithEventSpanningSeveralDays_ListsItOnEachDayItCovers()
    {
        var start = DateTime.Today.AddDays(-1);
        var spanning = NewEvent("Conference", start.AddHours(9), TimeSpan.FromHours(30));
        var other = NewEvent("Catch-up", DateTime.Today.AddHours(15), TimeSpan.FromHours(1));

        _vm.SelectDayCommand.Execute(Day(DateTime.Today, spanning, other));

        Assert.IsTrue(_vm.IsDayListVisible);
        Assert.AreEqual(2, _vm.SelectedDayEvents.Count);
        Assert.IsTrue(_vm.SelectedDayEvents.Contains(spanning));
    }

    // ── Closing the day list ───────────────────────────────────────

    [TestMethod]
    public void CloseDayList_HidesListAndClearsEvents()
    {
        OpenDayListWithTwoEvents();

        _vm.CloseDayListCommand.Execute(null);

        Assert.IsFalse(_vm.IsDayListVisible);
        Assert.AreEqual(0, _vm.SelectedDayEvents.Count);
        Assert.AreEqual("No events", _vm.SelectedDayCountLabel);
    }

    [TestMethod]
    public void NavigatePeriodOrSwitchView_ClosesDayList()
    {
        var commands = new Action[]
        {
            () => _vm.PreviousPeriodCommand.Execute(null),
            () => _vm.NextPeriodCommand.Execute(null),
            () => _vm.TodayCommand.Execute(null),
            () => _vm.SetViewCommand.Execute("Week")
        };

        foreach (var command in commands)
        {
            OpenDayListWithTwoEvents();
            Assert.IsTrue(_vm.IsDayListVisible);

            command();

            Assert.IsFalse(_vm.IsDayListVisible, "Navigating away must close the day list.");
        }
    }

    // ── System back press ──────────────────────────────────────────

    [TestMethod]
    public async Task HandleSystemBack_WhenDayListOpen_ClosesItAndConsumesThePress()
    {
        OpenDayListWithTwoEvents();
        Assert.IsTrue(_vm.CanHandleSystemBack);

        var handled = await _vm.HandleSystemBackAsync();

        Assert.IsTrue(handled);
        Assert.IsFalse(_vm.IsDayListVisible);
        Assert.IsFalse(_vm.CanHandleSystemBack);
    }

    [TestMethod]
    public async Task HandleSystemBack_WhenDayListClosed_LetsThePlatformHandleIt()
    {
        Assert.IsFalse(_vm.CanHandleSystemBack);
        Assert.IsFalse(await _vm.HandleSystemBackAsync());
    }

    // ── Staying in step with the server ────────────────────────────

    [TestMethod]
    public async Task LoadEvents_WhileDayListOpen_RefreshesTheDayThatIsShowing()
    {
        var day = DateTime.Today;
        var kept = NewEvent("Kept", day.AddHours(9), TimeSpan.FromHours(1));
        var deleted = NewEvent("Deleted", day.AddHours(11), TimeSpan.FromHours(1));
        var added = NewEvent("Added", day.AddHours(14), TimeSpan.FromHours(1));

        _vm.SelectDayCommand.Execute(Day(day, kept, deleted));
        Assert.AreEqual(2, _vm.SelectedDayEvents.Count);

        _calendarApi.Setup(c => c.ListEventsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CalendarEventDto> { added, kept });

        await _vm.LoadEventsCommand.ExecuteAsync(null);

        Assert.AreEqual(2, _vm.SelectedDayEvents.Count);
        Assert.IsTrue(_vm.SelectedDayEvents.Contains(added));
        Assert.IsTrue(_vm.SelectedDayEvents.Contains(kept));
        Assert.IsFalse(_vm.SelectedDayEvents.Contains(deleted));
    }

    [TestMethod]
    public async Task LoadEvents_OrdersTheDayViewListByStartTime()
    {
        var day = DateTime.Today;
        var late = NewEvent("Late", day.AddHours(16), TimeSpan.FromHours(1));
        var early = NewEvent("Early", day.AddHours(8), TimeSpan.FromHours(1));

        _calendarApi.Setup(c => c.ListEventsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CalendarEventDto> { late, early });

        await _vm.LoadEventsCommand.ExecuteAsync(null);

        CollectionAssert.AreEqual(new[] { early, late }, _vm.Events.ToArray());
    }

    // ── Day bucketing (OccursOn) ───────────────────────────────────

    [TestMethod]
    public void OccursOn_DayOutsideTheEvent_IsFalse()
    {
        var day = DateTime.Today;
        var evt = NewEvent("Single day", day.AddHours(10), TimeSpan.FromHours(1));

        Assert.IsFalse(CalendarViewModel.OccursOn(evt, day.AddDays(10)));
    }

    [TestMethod]
    public void OccursOn_EveryDayTheEventCovers_IsTrue()
    {
        var day = DateTime.Today;
        var evt = NewEvent("Long", day.AddHours(9), TimeSpan.FromHours(50));

        Assert.IsTrue(CalendarViewModel.OccursOn(evt, day));
        Assert.IsTrue(CalendarViewModel.OccursOn(evt, day.AddDays(1)));
    }

    [TestMethod]
    public void OccursOn_UnspecifiedKind_IsTreatedAsUtc()
    {
        // JSON deserialization returns DateTimeKind.Unspecified; treating that as local time would
        // shift every event by the device's UTC offset.
        var day = DateTime.Today;
        var evt = NewEvent("From JSON", day.AddHours(10), TimeSpan.FromHours(1));
        var fromJson = evt with
        {
            StartUtc = DateTime.SpecifyKind(evt.StartUtc, DateTimeKind.Unspecified),
            EndUtc = DateTime.SpecifyKind(evt.EndUtc, DateTimeKind.Unspecified)
        };

        for (var offset = -3; offset <= 3; offset++)
        {
            Assert.AreEqual(
                CalendarViewModel.OccursOn(evt, day.AddDays(offset)),
                CalendarViewModel.OccursOn(fromJson, day.AddDays(offset)),
                $"Day {offset} must bucket identically whether or not DateTime.Kind survived JSON.");
        }
    }

    // ── Helpers ────────────────────────────────────────────────────

    private void OpenDayListWithTwoEvents()
    {
        var day = DateTime.Today;
        _vm.SelectDayCommand.Execute(Day(
            day,
            NewEvent("One", day.AddHours(9), TimeSpan.FromHours(1)),
            NewEvent("Two", day.AddHours(11), TimeSpan.FromHours(1))));
    }

    private static CalendarDayItem Day(DateTime date, params CalendarEventDto[] events) =>
        new() { Date = date, IsCurrentMonth = true, Events = events };

    private static CalendarEventDto NewEvent(string title, DateTime startLocal, TimeSpan duration) => new()
    {
        Id = Guid.NewGuid(),
        CalendarId = CalendarId,
        CreatedByUserId = Guid.NewGuid(),
        Title = title,
        StartUtc = startLocal.ToUniversalTime(),
        EndUtc = startLocal.Add(duration).ToUniversalTime(),
        CreatedAt = startLocal.ToUniversalTime(),
        UpdatedAt = startLocal.ToUniversalTime()
    };
}
