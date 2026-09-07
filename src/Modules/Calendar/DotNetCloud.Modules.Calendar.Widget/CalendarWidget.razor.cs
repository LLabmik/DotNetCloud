using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services.ModuleApis;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Calendar.Widget;

/// <summary>
/// Home-page widget for the Calendar module: the signed-in user's upcoming events
/// over the next seven days.
/// </summary>
public partial class CalendarWidget : ComponentBase
{
    [Inject] private ICalendarApiClient ApiClient { get; set; } = default!;

    private readonly List<CalendarEventDto> _items = [];
    private bool _loading = true;
    private string? _error;
    private string EmptyMessage => "No upcoming events.";

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            var events = await ApiClient.GetUpcomingEventsAsync(now, now.AddDays(7), 5);
            _items.AddRange(events);
        }
        catch (Exception)
        {
            _error = "Unable to load calendar events.";
        }
        finally
        {
            _loading = false;
        }
    }

    private static string FormatStart(CalendarEventDto item)
    {
        var localStart = item.StartUtc.ToLocalTime();
        return item.IsAllDay
            ? localStart.ToString("MMM d, yyyy")
            : localStart.ToString("MMM d, yyyy h:mm tt");
    }

    /// <summary>
    /// Builds a deep link that opens the Calendar month view containing the given event.
    /// </summary>
    private static string HrefFor(CalendarEventDto item)
        => $"/apps/calendar?eventId={item.Id}";
}
