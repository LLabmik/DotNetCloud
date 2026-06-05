using System.Globalization;
using System.Security.Claims;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Calendar.Host.Protos;
using DotNetCloud.Modules.Calendar.Services;
using DotNetCloud.Modules.Contacts.Host.Protos;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Grpc.Clients;

/// <summary>
/// Options for the Calendar gRPC client used by the Core Server.
/// </summary>
public sealed class CalendarGrpcClientOptions
{
    /// <summary>The section name in appsettings.json.</summary>
    public const string SectionName = "CalendarGrpc";

    /// <summary>
    /// The gRPC address of the Calendar module (e.g., "http://localhost:5003",
    /// "unix:///run/dotnetcloud/dotnetcloud-calendar.sock").
    /// </summary>
    public string CalendarModuleAddress { get; set; } = "http://localhost:5003";

    /// <summary>Timeout for gRPC calls. Default: 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// gRPC implementation of <see cref="ICalendarApiClient"/>.
/// Calls the Calendar module's gRPC service instead of its REST API.
/// </summary>
public sealed class CalendarGrpcApiClient : ICalendarApiClient, IDisposable
{
    private readonly CalendarGrpcClientOptions _options;
    private readonly ModuleEndpointProvider _endpointProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CalendarGrpcApiClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;
    private readonly Lazy<CalendarGrpcService.CalendarGrpcServiceClient> _client;
    private readonly Lazy<GrpcChannel> _contactsChannel;
    private readonly Lazy<ContactsService.ContactsServiceClient> _contactsClient;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="CalendarGrpcApiClient"/> class.</summary>
    public CalendarGrpcApiClient(
        IOptions<CalendarGrpcClientOptions> options,
        ModuleEndpointProvider endpointProvider,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CalendarGrpcApiClient> logger)
    {
        _options = options.Value;
        _endpointProvider = endpointProvider;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _channel = new Lazy<GrpcChannel>(() => CreateChannel());
        _client = new Lazy<CalendarGrpcService.CalendarGrpcServiceClient>(
            () => new CalendarGrpcService.CalendarGrpcServiceClient(_channel.Value));
        _contactsChannel = new Lazy<GrpcChannel>(() => CreateChannel("dotnetcloud.contacts"));
        _contactsClient = new Lazy<ContactsService.ContactsServiceClient>(
            () => new ContactsService.ContactsServiceClient(_contactsChannel.Value));
    }

    private string GetUserId()
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(userId) ? userId : Guid.Empty.ToString();
    }

    // ─── Calendar CRUD ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarDto>> ListCalendarsAsync(CancellationToken cancellationToken = default)
        => (await SafeCallListAsync(async () =>
        {
            var request = new ListCalendarsRequest { UserId = GetUserId() };
            var response = await _client.Value.ListCalendarsAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return !response.Success ? (IReadOnlyList<CalendarDto>)[] : response.Calendars.Select(c => ToCalendarDto(c)!).Where(c => c is not null).Select(c => c!).ToList();
        }, "ListCalendars", Array.Empty<CalendarDto>()))!;

    /// <inheritdoc />
    public async Task<CalendarDto?> CreateCalendarAsync(CreateCalendarDto dto, CancellationToken cancellationToken = default)
        => await SafeCallAsync(async () =>
        {
            var request = new CreateCalendarRequest
            {
                UserId = GetUserId(),
                Name = dto.Name,
                Description = dto.Description ?? string.Empty,
                Color = dto.Color ?? string.Empty,
                Timezone = dto.Timezone ?? "UTC",
                OrganizationId = dto.OrganizationId?.ToString() ?? string.Empty
            };
            var response = await _client.Value.CreateCalendarAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return response.Success ? ToCalendarDto(response.Calendar) : null;
        }, "CreateCalendar");

    /// <inheritdoc />
    public async Task<CalendarDto?> UpdateCalendarAsync(Guid calendarId, UpdateCalendarDto dto, CancellationToken cancellationToken = default)
        => await SafeCallAsync(async () =>
        {
            var request = new UpdateCalendarRequest
            {
                CalendarId = calendarId.ToString(),
                UserId = GetUserId(),
                Name = dto.Name ?? string.Empty,
                Description = dto.Description ?? string.Empty,
                Color = dto.Color ?? string.Empty,
                Timezone = dto.Timezone ?? string.Empty,
                IsVisible = dto.IsVisible ?? true
            };
            var response = await _client.Value.UpdateCalendarAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return response.Success ? ToCalendarDto(response.Calendar) : null;
        }, "UpdateCalendar");

    /// <inheritdoc />
    public async Task DeleteCalendarAsync(Guid calendarId, CancellationToken cancellationToken = default)
        => await SafeCallAsync(async () =>
        {
            var request = new DeleteCalendarRequest { CalendarId = calendarId.ToString(), UserId = GetUserId() };
            await _client.Value.DeleteCalendarAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
        }, "DeleteCalendar");

    // ─── Event CRUD ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarEventDto>> ListEventsAsync(Guid calendarId, DateTime? startUtc, DateTime? endUtc, CancellationToken cancellationToken = default)
        => (await SafeCallListAsync(async () =>
        {
            var request = new ListEventsRequest
            {
                CalendarId = calendarId.ToString(),
                UserId = GetUserId(),
                FromUtc = startUtc?.ToString("O") ?? string.Empty,
                ToUtc = endUtc?.ToString("O") ?? string.Empty,
                Skip = 0,
                Take = 1000
            };
            var response = await _client.Value.ListEventsAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return !response.Success ? (IReadOnlyList<CalendarEventDto>)[] : response.Events.Select(e => ToEventDto(e)!).Where(e => e is not null).Select(e => e!).ToList();
        }, "ListEvents", Array.Empty<CalendarEventDto>()))!;

    /// <inheritdoc />
    public async Task<CalendarEventDto?> CreateEventAsync(CreateCalendarEventDto dto, CancellationToken cancellationToken = default)
        => await SafeCallAsync(async () =>
        {
            var request = ToCreateEventRequest(dto);
            var response = await _client.Value.CreateEventAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return response.Success ? ToEventDto(response.Event) : null;
        }, "CreateEvent");

    /// <inheritdoc />
    public async Task<CalendarEventDto?> UpdateEventAsync(Guid eventId, UpdateCalendarEventDto dto, CancellationToken cancellationToken = default)
        => await SafeCallAsync(async () =>
        {
            var request = ToUpdateEventRequest(eventId, dto);
            var response = await _client.Value.UpdateEventAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return response.Success ? ToEventDto(response.Event) : null;
        }, "UpdateEvent");

    /// <inheritdoc />
    public async Task<CalendarEventDto?> GetEventAsync(Guid eventId, CancellationToken cancellationToken = default)
        => await SafeCallAsync(async () =>
        {
            var request = new GetEventRequest { EventId = eventId.ToString(), UserId = GetUserId() };
            var response = await _client.Value.GetEventAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return response.Success ? ToEventDto(response.Event) : null;
        }, "GetEvent");

    /// <inheritdoc />
    public async Task DeleteEventAsync(Guid eventId, CancellationToken cancellationToken = default)
        => await SafeCallAsync(async () =>
        {
            var request = new DeleteEventRequest { EventId = eventId.ToString(), UserId = GetUserId() };
            await _client.Value.DeleteEventAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
        }, "DeleteEvent");

    // ─── RSVP ───────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<CalendarEventDto?> RsvpAsync(Guid eventId, EventRsvpDto rsvp, CancellationToken cancellationToken = default)
        => await SafeCallAsync(async () =>
        {
            var request = new RsvpRequest { EventId = eventId.ToString(), UserId = GetUserId(), Status = rsvp.Status.ToString(), Comment = rsvp.Comment ?? string.Empty };
            var response = await _client.Value.RsvpAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return response.Success ? ToEventDto(response.Event) : null;
        }, "Rsvp");

    // ─── Search ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarEventDto>> SearchEventsAsync(string? query, DateTime? from, DateTime? to, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
        => (await SafeCallListAsync(async () =>
        {
            var request = new SearchEventsRequest
            {
                UserId = GetUserId(),
                Query = query ?? string.Empty,
                FromUtc = from?.ToString("O") ?? string.Empty,
                ToUtc = to?.ToString("O") ?? string.Empty,
                Skip = skip,
                Take = take
            };
            var response = await _client.Value.SearchEventsAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return !response.Success ? (IReadOnlyList<CalendarEventDto>)[] : response.Events.Select(e => ToEventDto(e)!).Where(e => e is not null).Select(e => e!).ToList();
        }, "SearchEvents", Array.Empty<CalendarEventDto>()))!;

    // ─── Sharing ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarShareResponse>> ListSharesAsync(Guid calendarId, CancellationToken cancellationToken = default)
        => (await SafeCallListAsync(async () =>
        {
            var request = new ListCalendarSharesRequest { CalendarId = calendarId.ToString(), UserId = GetUserId() };
            var response = await _client.Value.ListCalendarSharesAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return !response.Success ? (IReadOnlyList<CalendarShareResponse>)[] : response.Shares.Select(s => new CalendarShareResponse
            {
                Id = Guid.Parse(s.Id),
                CalendarId = Guid.Parse(s.CalendarId),
                SharedWithUserId = string.IsNullOrEmpty(s.SharedWithUserId) ? null : Guid.Parse(s.SharedWithUserId),
                SharedWithTeamId = string.IsNullOrEmpty(s.SharedWithTeamId) ? null : Guid.Parse(s.SharedWithTeamId),
                Permission = s.Permission,
                CreatedAt = DateTime.Parse(s.CreatedAt),
                CreatedByUserId = string.IsNullOrEmpty(s.SharedByUserId) ? null : Guid.Parse(s.SharedByUserId)
            }).ToList();
        }, "ListShares", Array.Empty<CalendarShareResponse>()))!;

    /// <inheritdoc />
    public async Task<CalendarShareResponse?> ShareCalendarAsync(Guid calendarId, Guid? userId, Guid? teamId, string permission = "ReadOnly", CancellationToken cancellationToken = default)
        => await SafeCallAsync(async () =>
        {
            var request = new ShareCalendarRequest
            {
                CalendarId = calendarId.ToString(),
                UserId = GetUserId(),
                TargetUserId = userId?.ToString() ?? string.Empty,
                TeamId = teamId?.ToString() ?? string.Empty,
                Permission = permission
            };
            var response = await _client.Value.ShareCalendarAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            if (!response.Success || response.Share is null)
                return null;
            var s = response.Share;
            return new CalendarShareResponse
            {
                Id = Guid.Parse(s.Id),
                CalendarId = Guid.Parse(s.CalendarId),
                SharedWithUserId = string.IsNullOrEmpty(s.SharedWithUserId) ? null : Guid.Parse(s.SharedWithUserId),
                SharedWithTeamId = string.IsNullOrEmpty(s.SharedWithTeamId) ? null : Guid.Parse(s.SharedWithTeamId),
                Permission = s.Permission,
                CreatedAt = DateTime.Parse(s.CreatedAt),
                CreatedByUserId = string.IsNullOrEmpty(s.SharedByUserId) ? null : Guid.Parse(s.SharedByUserId)
            };
        }, "ShareCalendar");

    /// <inheritdoc />
    public async Task RevokeShareAsync(Guid shareId, CancellationToken cancellationToken = default)
        => await SafeCallAsync(async () =>
        {
            var request = new RevokeCalendarShareRequest { ShareId = shareId.ToString(), UserId = GetUserId() };
            await _client.Value.RevokeCalendarShareAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
        }, "RevokeShare");

    // ─── Import/Export ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<string> ExportCalendarICalAsync(Guid calendarId, CancellationToken cancellationToken = default)
        => (await SafeCallAsync(async () =>
        {
            var request = new ExportCalendarICalRequest { CalendarId = calendarId.ToString(), UserId = GetUserId() };
            var response = await _client.Value.ExportCalendarICalAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return response.Success ? response.IcalText : string.Empty;
        }, "ExportCalendarICal", string.Empty))!;

    /// <inheritdoc />
    public async Task ImportICalAsync(Guid calendarId, string iCalText, CancellationToken cancellationToken = default)
        => await SafeCallAsync(async () =>
        {
            var request = new ImportICalRequest { UserId = GetUserId(), CalendarId = calendarId.ToString(), IcalText = iCalText };
            await _client.Value.ImportICalAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
        }, "ImportICal");

    // ─── Contact Search ─────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContactSearchResultDto>> SearchContactsAsync(
        string query, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];
        try
        {
            var request = new SearchContactsRequest
            {
                UserId = GetUserId(),
                Query = query.Trim(),
                MaxResults = maxResults
            };
            var response = await _contactsClient.Value.SearchContactsAsync(
                request, DeadlineHeaders(cancellationToken)).ResponseAsync;

            if (!response.Success)
                return [];
            return response.Results.Select(r => new ContactSearchResultDto
            {
                ContactId = Guid.Parse(r.ContactId),
                DisplayName = r.DisplayName,
                Emails = r.Emails.Select(e => (e.Address, e.Label)).ToList()
            }).ToList();
        }
        catch (RpcException ex) when (ex is { StatusCode: StatusCode.Unavailable } or { StatusCode: StatusCode.DeadlineExceeded })
        {
            _logger.LogWarning("Calendar SearchContacts gRPC failed: {Status}", ex.StatusCode);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Calendar SearchContacts error");
            return [];
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<T> SafeCallListAsync<T>(Func<Task<T>> call, string operation, T fallback) where T : class
    {
        try
        { return await call(); }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        { _logger.LogWarning("Calendar {Op} gRPC unavailable", operation); }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        { _logger.LogWarning("Calendar {Op} gRPC timed out", operation); }
        catch (Exception ex)
        { _logger.LogError(ex, "Calendar {Op} unexpected error", operation); }
        return fallback;
    }

    private async Task<T?> SafeCallAsync<T>(Func<Task<T?>> call, string operation, T? fallback = default)
    {
        try
        { return await call(); }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        { _logger.LogWarning("Calendar {Op} gRPC unavailable", operation); }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        { _logger.LogWarning("Calendar {Op} gRPC timed out", operation); }
        catch (Exception ex)
        { _logger.LogError(ex, "Calendar {Op} unexpected error", operation); }
        return fallback;
    }

    private async Task SafeCallAsync(Func<Task> call, string operation)
    {
        try
        { await call(); }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        { _logger.LogWarning("Calendar {Op} gRPC unavailable", operation); }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        { _logger.LogWarning("Calendar {Op} gRPC timed out", operation); }
        catch (Exception ex)
        { _logger.LogError(ex, "Calendar {Op} unexpected error", operation); }
    }

    private CallOptions DeadlineHeaders(CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.Add(_options.Timeout);
        return new CallOptions(deadline: deadline, cancellationToken: ct);
    }

    private GrpcChannel CreateChannel(string moduleId = "dotnetcloud.calendar")
    {
        var address = _endpointProvider.GetEndpoint(moduleId);
        _logger.LogInformation("CalendarGrpcApiClient channel to {ModuleId} at {Address}", moduleId, address);
        return GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                ConnectTimeout = TimeSpan.FromSeconds(5)
            }
        });
    }

    private CreateEventRequest ToCreateEventRequest(CreateCalendarEventDto dto) => new()
    {
        UserId = GetUserId(),
        CalendarId = dto.CalendarId.ToString(),
        Title = dto.Title,
        Description = dto.Description ?? string.Empty,
        Location = dto.Location ?? string.Empty,
        StartUtc = dto.StartUtc.ToString("O"),
        EndUtc = dto.EndUtc.ToString("O"),
        IsAllDay = dto.IsAllDay,
        RecurrenceRule = dto.RecurrenceRule ?? string.Empty,
        Color = dto.Color ?? string.Empty,
        Url = dto.Url ?? string.Empty,
        Attendees = { dto.Attendees.Select(a => new AttendeeMessage { UserId = a.UserId?.ToString() ?? string.Empty, Email = a.Email, DisplayName = a.DisplayName ?? string.Empty, Role = a.Role.ToString(), Status = a.Status.ToString() }) },
        Reminders = { dto.Reminders.Select(r => new ReminderMessage { Method = r.Method.ToString(), MinutesBefore = r.MinutesBefore }) }
    };

    private UpdateEventRequest ToUpdateEventRequest(Guid eventId, UpdateCalendarEventDto dto) => new()
    {
        EventId = eventId.ToString(),
        UserId = GetUserId(),
        Title = dto.Title ?? string.Empty,
        Description = dto.Description ?? string.Empty,
        Location = dto.Location ?? string.Empty,
        StartUtc = dto.StartUtc?.ToString("O") ?? string.Empty,
        EndUtc = dto.EndUtc?.ToString("O") ?? string.Empty,
        IsAllDay = dto.IsAllDay?.ToString() ?? string.Empty,
        Status = dto.Status?.ToString() ?? string.Empty,
        RecurrenceRule = dto.RecurrenceRule ?? string.Empty,
        Color = dto.Color ?? string.Empty,
        Url = dto.Url ?? string.Empty,
        Attendees = { dto.Attendees?.Select(a => new AttendeeMessage { UserId = a.UserId?.ToString() ?? string.Empty, Email = a.Email, DisplayName = a.DisplayName ?? string.Empty, Role = a.Role.ToString(), Status = a.Status.ToString() }) ?? [] },
        Reminders = { dto.Reminders?.Select(r => new ReminderMessage { Method = r.Method.ToString(), MinutesBefore = r.MinutesBefore }) ?? [] }
    };

    private static CalendarDto? ToCalendarDto(CalendarMessage? c)
    {
        if (c is null)
            return null;
        try
        {
            return new CalendarDto
            {
                Id = Guid.Parse(c.Id),
                OwnerId = Guid.Parse(c.OwnerId),
                Name = c.Name,
                Description = string.IsNullOrEmpty(c.Description) ? null : c.Description,
                Color = string.IsNullOrEmpty(c.Color) ? null : c.Color,
                Timezone = c.Timezone,
                IsDefault = c.IsDefault,
                IsVisible = c.IsVisible,
                SyncToken = string.IsNullOrEmpty(c.SyncToken) ? null : c.SyncToken,
                CreatedAt = DateTime.Parse(c.CreatedAt),
                UpdatedAt = DateTime.Parse(c.UpdatedAt),
                OrganizationId = string.IsNullOrEmpty(c.OrganizationId) ? null : Guid.Parse(c.OrganizationId)
            };
        }
        catch { return null; }
    }

    private static CalendarEventDto? ToEventDto(EventMessage? e)
    {
        if (e is null)
            return null;
        try
        {
            return new CalendarEventDto
            {
                Id = Guid.Parse(e.Id),
                CalendarId = Guid.Parse(e.CalendarId),
                CreatedByUserId = Guid.Parse(e.CreatedByUserId),
                Title = e.Title,
                Description = string.IsNullOrEmpty(e.Description) ? null : e.Description,
                Location = string.IsNullOrEmpty(e.Location) ? null : e.Location,
                StartUtc = DateTime.Parse(e.StartUtc),
                EndUtc = DateTime.Parse(e.EndUtc),
                IsAllDay = e.IsAllDay,
                Status = Enum.TryParse<CalendarEventStatus>(e.Status, out var status) ? status : CalendarEventStatus.Confirmed,
                RecurrenceRule = string.IsNullOrEmpty(e.RecurrenceRule) ? null : e.RecurrenceRule,
                RecurringEventId = string.IsNullOrEmpty(e.RecurringEventId) ? null : Guid.Parse(e.RecurringEventId),
                OriginalStartUtc = string.IsNullOrEmpty(e.OriginalStartUtc) ? null : DateTime.Parse(e.OriginalStartUtc),
                Color = string.IsNullOrEmpty(e.Color) ? null : e.Color,
                Url = string.IsNullOrEmpty(e.Url) ? null : e.Url,
                ETag = string.IsNullOrEmpty(e.Etag) ? null : e.Etag,
                CreatedAt = DateTime.Parse(e.CreatedAt),
                UpdatedAt = DateTime.Parse(e.UpdatedAt),
                Attendees = e.Attendees.Select(a => new EventAttendeeDto { UserId = string.IsNullOrEmpty(a.UserId) ? null : Guid.Parse(a.UserId), Email = a.Email, DisplayName = string.IsNullOrEmpty(a.DisplayName) ? null : a.DisplayName, Role = Enum.TryParse<AttendeeRole>(a.Role, out var role) ? role : AttendeeRole.Required, Status = Enum.TryParse<AttendeeStatus>(a.Status, out var attStatus) ? attStatus : AttendeeStatus.NeedsAction }).ToList(),
                Reminders = e.Reminders.Select(r => new EventReminderDto { Method = Enum.TryParse<ReminderMethod>(r.Method, out var method) ? method : ReminderMethod.Notification, MinutesBefore = r.MinutesBefore }).ToList()
            };
        }
        catch { return null; }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        try
        { if (_channel.IsValueCreated) _channel.Value.Dispose(); }
        catch { /* ignore */ }
        try
        { if (_contactsChannel.IsValueCreated) _contactsChannel.Value.Dispose(); }
        catch { /* ignore */ }
    }
}
