using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Calendar.Models;

namespace DotNetCloud.Modules.Calendar.Services;

/// <summary>
/// Calendar sharing operations.
/// </summary>
public interface ICalendarShareService
{
    /// <summary>Shares a calendar with a user or team.</summary>
    Task<CalendarShare> ShareCalendarAsync(Guid calendarId, Guid? userId, Guid? teamId, CalendarSharePermission permission, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Removes a share.</summary>
    Task RemoveShareAsync(Guid shareId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists shares for a calendar.</summary>
    Task<IReadOnlyList<CalendarShare>> ListSharesAsync(Guid calendarId, CallerContext caller, CancellationToken cancellationToken = default);
}
