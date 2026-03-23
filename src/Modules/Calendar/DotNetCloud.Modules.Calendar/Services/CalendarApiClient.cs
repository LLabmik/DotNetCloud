using System.Net.Http.Json;
using System.Text.Json;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Calendar.Services;

/// <summary>
/// HTTP implementation of <see cref="ICalendarApiClient"/>.
/// </summary>
public sealed class CalendarApiClient : ICalendarApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CalendarApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<CalendarDto>> ListCalendarsAsync(CancellationToken cancellationToken = default)
    {
        return await ReadDataAsync<IReadOnlyList<CalendarDto>>("api/v1/calendars", cancellationToken) ?? [];
    }

    public async Task<CalendarDto?> CreateCalendarAsync(CreateCalendarDto dto, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/calendars", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadDataAsync<CalendarDto>(response, cancellationToken);
    }

    public async Task<CalendarDto?> UpdateCalendarAsync(Guid calendarId, UpdateCalendarDto dto, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/calendars/{calendarId}", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadDataAsync<CalendarDto>(response, cancellationToken);
    }

    public async Task DeleteCalendarAsync(Guid calendarId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/calendars/{calendarId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<CalendarEventDto>> ListEventsAsync(Guid calendarId, DateTime? startUtc, DateTime? endUtc, CancellationToken cancellationToken = default)
    {
        var url = $"api/v1/calendars/{calendarId}/events";
        var query = new List<string>();
        if (startUtc.HasValue)
        {
            query.Add($"startUtc={Uri.EscapeDataString(startUtc.Value.ToString("O"))}");
        }

        if (endUtc.HasValue)
        {
            query.Add($"endUtc={Uri.EscapeDataString(endUtc.Value.ToString("O"))}");
        }

        if (query.Count > 0)
        {
            url += $"?{string.Join("&", query)}";
        }

        return await ReadDataAsync<IReadOnlyList<CalendarEventDto>>(url, cancellationToken) ?? [];
    }

    public async Task<CalendarEventDto?> CreateEventAsync(CreateCalendarEventDto dto, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/calendars/events", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadDataAsync<CalendarEventDto>(response, cancellationToken);
    }

    public async Task<CalendarEventDto?> UpdateEventAsync(Guid eventId, UpdateCalendarEventDto dto, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/calendars/events/{eventId}", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadDataAsync<CalendarEventDto>(response, cancellationToken);
    }

    public async Task DeleteEventAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/calendars/events/{eventId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<CalendarEventDto?> RsvpAsync(Guid eventId, EventRsvpDto rsvp, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/calendars/events/{eventId}/rsvp", rsvp, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadDataAsync<CalendarEventDto>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<CalendarEventDto>> SearchEventsAsync(string? query, DateTime? from, DateTime? to, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var url = $"api/v1/calendars/events/search?skip={skip}&take={take}";
        if (!string.IsNullOrWhiteSpace(query))
        {
            url += $"&q={Uri.EscapeDataString(query)}";
        }

        if (from.HasValue)
        {
            url += $"&from={Uri.EscapeDataString(from.Value.ToString("O"))}";
        }

        if (to.HasValue)
        {
            url += $"&to={Uri.EscapeDataString(to.Value.ToString("O"))}";
        }

        return await ReadDataAsync<IReadOnlyList<CalendarEventDto>>(url, cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<CalendarShareResponse>> ListSharesAsync(Guid calendarId, CancellationToken cancellationToken = default)
    {
        return await ReadDataAsync<IReadOnlyList<CalendarShareResponse>>($"api/v1/calendars/{calendarId}/shares", cancellationToken) ?? [];
    }

    public async Task<CalendarShareResponse?> ShareCalendarAsync(Guid calendarId, Guid? userId, Guid? teamId, string permission = "ReadOnly", CancellationToken cancellationToken = default)
    {
        var body = new { UserId = userId, TeamId = teamId, Permission = permission };
        var response = await _httpClient.PostAsJsonAsync($"api/v1/calendars/{calendarId}/shares", body, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadDataAsync<CalendarShareResponse>(response, cancellationToken);
    }

    public async Task RevokeShareAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/calendars/shares/{shareId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string> ExportCalendarICalAsync(Guid calendarId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/v1/calendars/{calendarId}/export", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task ImportICalAsync(Guid calendarId, string iCalText, CancellationToken cancellationToken = default)
    {
        var body = new { ICalText = iCalText };
        var response = await _httpClient.PostAsJsonAsync($"api/v1/calendars/{calendarId}/import", body, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<T?> ReadDataAsync<T>(string url, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadDataAsync<T>(response, cancellationToken);
    }

    private static async Task<T?> ReadDataAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(json);

        if (!TryGetPropertyIgnoreCase(document.RootElement, "data", out var dataElement))
        {
            return default;
        }

        if (dataElement.ValueKind == JsonValueKind.Object && TryGetPropertyIgnoreCase(dataElement, "data", out var nestedData))
        {
            dataElement = nestedData;
        }

        return JsonSerializer.Deserialize<T>(dataElement.GetRawText(), JsonOptions);
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
