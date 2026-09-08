using DotNetCloud.Core.SharedWithMe;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.SharedWithMe;

/// <summary>
/// Adapts the Calendar module's "shared with me" REST query (via the in-process HTTP module
/// client <see cref="ICalendarApiClient"/>) into an <see cref="ISharedWithMeProvider"/> so Files
/// can surface shared calendars as virtual deep-link entries under
/// <c>_DotNetCloud/SharedWithMe/Calendar</c>.
/// </summary>
/// <remarks>
/// Calendar is process-isolated; Core.Server talks to its host through the module HTTP client
/// (the same channel the CalendarPage UI uses, which forwards the caller's auth cookie). A fresh
/// DI scope is created per call. Module failures degrade gracefully to an empty list rather than
/// throwing into the Files listing.
/// </remarks>
public sealed class CalendarSharedWithMeProvider : ISharedWithMeProvider
{
    /// <summary>Stable module id for Calendar.</summary>
    public const string CalendarModuleId = "calendar";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CalendarSharedWithMeProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarSharedWithMeProvider"/> class.
    /// </summary>
    /// <param name="scopeFactory">Factory used to create a DI scope per call.</param>
    /// <param name="logger">Logger for graceful module-failure diagnostics.</param>
    public CalendarSharedWithMeProvider(IServiceScopeFactory scopeFactory, ILogger<CalendarSharedWithMeProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ModuleId => CalendarModuleId;

    /// <inheritdoc />
    public string DisplayName => "Calendar";

    /// <inheritdoc />
    public string IconName => "calendar_today";

    /// <inheritdoc />
    public async Task<int> CountAsync(Guid userId, CancellationToken cancellationToken = default)
        => (await ListAsync(userId, cancellationToken)).Count;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SharedWithMeModuleItem>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<ICalendarApiClient>();

        IReadOnlyList<CalendarSharedItem> items;
        try
        {
            items = await client.ListSharedWithMeAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Calendar shared-with-me lookup failed for {UserId}; returning empty", userId);
            return [];
        }

        return MapItems(userId, items);
    }

    /// <summary>
    /// Maps the calendars shared with <paramref name="userId"/> to shared-with-me items. Calendars
    /// the user owns (or shared themselves to a team they belong to) are excluded so only genuine
    /// inbound shares are surfaced.
    /// </summary>
    /// <param name="userId">The recipient user id.</param>
    /// <param name="items">Shared calendars visible to the caller (user + team).</param>
    internal static IReadOnlyList<SharedWithMeModuleItem> MapItems(Guid userId, IEnumerable<CalendarSharedItem> items)
        => items
            .Where(item =>
                item.OwnerId != userId &&
                item.CreatedByUserId != userId &&
                !string.IsNullOrWhiteSpace(item.Name))
            .Select(item => new SharedWithMeModuleItem(
                ModuleId: CalendarModuleId,
                DisplayName: "Calendar",
                EntityId: item.CalendarId,
                EntityType: "Calendar",
                Title: item.Name,
                Subtitle: "Calendar",
                DeepLink: $"/apps/calendar?calendarId={item.CalendarId}",
                IconName: "calendar_today",
                UpdatedAt: item.UpdatedAt == default ? item.CreatedAt : item.UpdatedAt))
            .ToList();
}
