using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Calendar view showing work items by due date.
/// Supports month and week views with drag-to-reschedule.
/// </summary>
public partial class WorkItemCalendarView : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    /// <summary>The product to display calendar for.</summary>
    [Parameter, EditorRequired] public ProductDto Product { get; set; } = default!;

    /// <summary>All swimlanes in the current view.</summary>
    [Parameter] public List<SwimlaneDto> Swimlanes { get; set; } = [];

    /// <summary>Work items grouped by swimlane.</summary>
    [Parameter] public Dictionary<Guid, List<WorkItemDto>> WorkItemsBySwimlane { get; set; } = [];

    /// <summary>Called when a work item is selected (by item number).</summary>
    [Parameter] public EventCallback<int> OnWorkItemSelected { get; set; }

    /// <summary>Called when a work item's due date is changed via drag-and-drop.</summary>
    [Parameter] public EventCallback OnItemsChanged { get; set; }

    private enum CalendarViewMode { Month, Week }

    private CalendarViewMode _viewMode = CalendarViewMode.Month;
    private DateTime _currentMonth;
    private string _currentMonthName => _currentMonth.ToString("MMMM");
    private int _currentYear => _currentMonth.Year;
    private bool _isLoading = true;

    private readonly List<CalendarDay> _calendarDays = [];
    private readonly List<CalendarDay> _weekDays = [];

    private static readonly string[] _dayNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        await BuildCalendarAsync();
    }

    private async Task BuildCalendarAsync()
    {
        _isLoading = true;
        _calendarDays.Clear();
        _weekDays.Clear();

        // Collect all work items with due dates
        var allItems = WorkItemsBySwimlane.Values
            .SelectMany(items => items)
            .Where(item => item.DueDate.HasValue)
            .OrderBy(item => item.DueDate)
            .ToList();

        // Build month grid
        var firstDayOfMonth = new DateTime(_currentMonth.Year, _currentMonth.Month, 1);
        var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
        var startDay = firstDayOfMonth.AddDays(-(int)firstDayOfMonth.DayOfWeek);
        var endDay = lastDayOfMonth.AddDays(6 - (int)lastDayOfMonth.DayOfWeek);

        var today = DateTime.Today;

        for (var date = startDay; date <= endDay; date = date.AddDays(1))
        {
            var isCurrentMonth = date.Month == _currentMonth.Month && date.Year == _currentMonth.Year;
            var day = new CalendarDay(date, date.Date == today, isCurrentMonth);

            var itemsForDay = allItems
                .Where(item => item.DueDate?.Date == date.Date)
                .Select(item => new WorkItemCalendarEntry
                {
                    ItemNumber = item.ItemNumber,
                    Title = item.Title,
                    Priority = item.Priority,
                    DueDate = item.DueDate
                })
                .ToList();

            day.Items.AddRange(itemsForDay);
            _calendarDays.Add(day);
        }

        // Build week view (current week)
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        for (int i = 0; i < 7; i++)
        {
            var date = weekStart.AddDays(i);
            var day = new CalendarDay(date, date.Date == today, true);
            var itemsForDay = allItems
                .Where(item => item.DueDate?.Date == date.Date)
                .Select(item => new WorkItemCalendarEntry
                {
                    ItemNumber = item.ItemNumber,
                    Title = item.Title,
                    Priority = item.Priority,
                    DueDate = item.DueDate
                })
                .ToList();
            day.Items.AddRange(itemsForDay);
            _weekDays.Add(day);
        }

        _isLoading = false;
    }

    private void SetViewMode(CalendarViewMode mode)
    {
        _viewMode = mode;
    }

    private async Task GoToPrevious()
    {
        _currentMonth = _currentMonth.AddMonths(-1);
        await BuildCalendarAsync();
    }

    private async Task GoToNext()
    {
        _currentMonth = _currentMonth.AddMonths(1);
        await BuildCalendarAsync();
    }

    private async Task GoToToday()
    {
        _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        await BuildCalendarAsync();
    }

    private void HandleDragStart(WorkItemCalendarEntry item)
    {
        // Drag start is handled by HTML5 drag events — store item number for drop
        _dragItemNumber = item.ItemNumber;
    }

    private int _dragItemNumber;

    private async Task HandleDropOnDate(DateTime date)
    {
        if (_dragItemNumber <= 0) return;

        try
        {
            // Find the work item by number and update its due date
            var allItems = WorkItemsBySwimlane.Values.SelectMany(i => i);
            var item = allItems.FirstOrDefault(i => i.ItemNumber == _dragItemNumber);
            if (item is not null)
            {
                var dueDate = new DateTime(date.Year, date.Month, date.Day, 12, 0, 0, DateTimeKind.Utc);
                await ApiClient.UpdateWorkItemAsync(item.Id, new UpdateWorkItemDto { DueDate = dueDate });
                await OnItemsChanged.InvokeAsync();
                await BuildCalendarAsync();
            }
        }
        catch
        {
            // Drag failed silently
        }
        finally
        {
            _dragItemNumber = 0;
        }
    }

    private static string GetPriorityClass(Priority priority) => priority switch
    {
        Priority.Urgent => "urgent",
        Priority.High => "high",
        Priority.Medium => "medium",
        Priority.Low => "low",
        _ => ""
    };

    private sealed record CalendarDay(DateTime Date, bool IsToday, bool IsCurrentMonth)
    {
        public List<WorkItemCalendarEntry> Items { get; } = [];
    }

    private sealed record WorkItemCalendarEntry
    {
        public int ItemNumber { get; init; }
        public string Title { get; init; } = "";
        public Priority Priority { get; init; }
        public DateTime? DueDate { get; init; }
    }
}
