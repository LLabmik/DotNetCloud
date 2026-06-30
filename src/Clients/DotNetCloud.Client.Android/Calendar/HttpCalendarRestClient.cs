using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.Calendar;

/// <summary>
/// <see cref="ICalendarRestClient"/> implementation backed by <see cref="HttpClient"/>.
/// Registered via <c>AddHttpClient&lt;ICalendarRestClient, HttpCalendarRestClient&gt;()</c>.
/// </summary>
internal sealed class HttpCalendarRestClient : ICalendarRestClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;

    /// <summary>Initializes a new <see cref="HttpCalendarRestClient"/>.</summary>
    public HttpCalendarRestClient(HttpClient http)
    {
        _http = http;
    }

    // ── Calendars ──────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarDto>> ListCalendarsAsync(
        string serverBaseUrl, string accessToken,
        CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var result = await GetEnvelopeDataAsync<List<CalendarDto>>(
            $"{Url(serverBaseUrl)}/api/v1/calendars", ct).ConfigureAwait(false);
        return result ?? [];
    }

    /// <inheritdoc />
    public async Task<CalendarDto> GetCalendarAsync(
        string serverBaseUrl, string accessToken,
        Guid calendarId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        return await GetEnvelopeDataAsync<CalendarDto>(
            $"{Url(serverBaseUrl)}/api/v1/calendars/{calendarId}", ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Calendar {calendarId} not found.");
    }

    /// <inheritdoc />
    public async Task<CalendarDto> CreateCalendarAsync(
        string serverBaseUrl, string accessToken,
        CreateCalendarDto dto, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        using var response = await _http.PostAsJsonAsync(
            $"{Url(serverBaseUrl)}/api/v1/calendars", dto, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadEnvelopeDataAsync<CalendarDto>(response, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Server returned null for calendar creation.");
    }

    /// <inheritdoc />
    public async Task<CalendarDto> UpdateCalendarAsync(
        string serverBaseUrl, string accessToken,
        Guid calendarId, UpdateCalendarDto dto, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        using var response = await _http.PutAsJsonAsync(
            $"{Url(serverBaseUrl)}/api/v1/calendars/{calendarId}", dto, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadEnvelopeDataAsync<CalendarDto>(response, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Server returned null for calendar update.");
    }

    /// <inheritdoc />
    public async Task DeleteCalendarAsync(
        string serverBaseUrl, string accessToken,
        Guid calendarId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        using var response = await _http.DeleteAsync(
            $"{Url(serverBaseUrl)}/api/v1/calendars/{calendarId}", ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    // ── Events ─────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarEventDto>> ListEventsAsync(
        string serverBaseUrl, string accessToken,
        Guid calendarId, DateTime? from = null, DateTime? to = null,
        int skip = 0, int take = 200, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var query = $"?skip={skip}&take={take}";
        if (from.HasValue) query += $"&from={from.Value:O}";
        if (to.HasValue) query += $"&to={to.Value:O}";

        var result = await GetEnvelopeDataAsync<List<CalendarEventDto>>(
            $"{Url(serverBaseUrl)}/api/v1/calendars/{calendarId}/events{query}", ct)
            .ConfigureAwait(false);
        return result ?? [];
    }

    /// <inheritdoc />
    public async Task<CalendarEventDto> GetEventAsync(
        string serverBaseUrl, string accessToken,
        Guid eventId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        return await GetEnvelopeDataAsync<CalendarEventDto>(
            $"{Url(serverBaseUrl)}/api/v1/calendars/events/{eventId}", ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Event {eventId} not found.");
    }

    /// <inheritdoc />
    public async Task<CalendarEventDto> CreateEventAsync(
        string serverBaseUrl, string accessToken,
        CreateCalendarEventDto dto, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        using var response = await _http.PostAsJsonAsync(
            $"{Url(serverBaseUrl)}/api/v1/calendars/events", dto, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadEnvelopeDataAsync<CalendarEventDto>(response, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Server returned null for event creation.");
    }

    /// <inheritdoc />
    public async Task<CalendarEventDto> UpdateEventAsync(
        string serverBaseUrl, string accessToken,
        Guid eventId, UpdateCalendarEventDto dto, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        using var response = await _http.PutAsJsonAsync(
            $"{Url(serverBaseUrl)}/api/v1/calendars/events/{eventId}", dto, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadEnvelopeDataAsync<CalendarEventDto>(response, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Server returned null for event update.");
    }

    /// <inheritdoc />
    public async Task DeleteEventAsync(
        string serverBaseUrl, string accessToken,
        Guid eventId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        using var response = await _http.DeleteAsync(
            $"{Url(serverBaseUrl)}/api/v1/calendars/events/{eventId}", ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task<CalendarEventDto> RsvpAsync(
        string serverBaseUrl, string accessToken,
        Guid eventId, EventRsvpDto dto, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        using var response = await _http.PostAsJsonAsync(
            $"{Url(serverBaseUrl)}/api/v1/calendars/events/{eventId}/rsvp", dto, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadEnvelopeDataAsync<CalendarEventDto>(response, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Server returned null for RSVP.");
    }

    // ── Search ─────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarEventDto>> SearchEventsAsync(
        string serverBaseUrl, string accessToken,
        string query, DateTime? from = null, DateTime? to = null,
        int skip = 0, int take = 200, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var q = $"?q={Uri.EscapeDataString(query)}&skip={skip}&take={take}";
        if (from.HasValue) q += $"&from={from.Value:O}";
        if (to.HasValue) q += $"&to={to.Value:O}";

        var result = await GetEnvelopeDataAsync<List<CalendarEventDto>>(
            $"{Url(serverBaseUrl)}/api/v1/calendars/events/search{q}", ct)
            .ConfigureAwait(false);
        return result ?? [];
    }

    // ── Private helpers ─────────────────────────────────────────────

    private void SetAuth(string accessToken) =>
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

    private static string Url(string serverBaseUrl) => serverBaseUrl.TrimEnd('/');

    private async Task<T?> GetEnvelopeDataAsync<T>(string url, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadEnvelopeDataAsync<T>(response, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the response body, unwrapping the server's standard envelope
    /// (<c>{"success":true,"data":...}</c>) if present.
    /// </summary>
    private static async Task<T?> ReadEnvelopeDataAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);

        if (doc.RootElement.ValueKind == JsonValueKind.Object &&
            doc.RootElement.TryGetProperty("data", out var dataProp))
        {
            return dataProp.Deserialize<T>(JsonOpts);
        }

        return doc.RootElement.Deserialize<T>(JsonOpts);
    }
}
